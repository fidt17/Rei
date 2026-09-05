using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Tests.Models.Services.Render;

/// <summary>Verifies conversion between shader uniforms, editable property trees, and persisted material values.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "MaterialProperties")]
public sealed class MaterialShaderPropertyUtilsTests
{
    /// <summary>Scalar uniforms parse supported text and use zero defaults for absent or invalid values.</summary>
    [Theory]
    [InlineData(ShaderUniformType.Float, "1.5", 1.5)]
    [InlineData(ShaderUniformType.Float, null, 0)]
    [InlineData(ShaderUniformType.Float, "invalid", 0)]
    [InlineData(ShaderUniformType.Integer, "12", 12)]
    [InlineData(ShaderUniformType.Integer, "invalid", 0)]
    public void ScalarUniformsCreateTypedEditableValues(ShaderUniformType type, string? input, double expected)
    {
        var uniform = new ShaderUniformInfo("strength", type == ShaderUniformType.Float ? "float" : "int", type);
        var property = MaterialShaderPropertyUtils.CreateSerializedProperty(uniform, input);

        Assert.Equal("strength", property.Name);
        Assert.Equal(uniform.SourceType, property.SourceType);
        Assert.Equal(type == ShaderUniformType.Float ? SerializedTypeEnum.Float : SerializedTypeEnum.Integer, property.Type);
        if (type == ShaderUniformType.Float) Assert.Equal((float)expected, Assert.IsType<float>(property.Value));
        else Assert.Equal((int)expected, Assert.IsType<int>(property.Value));
        Assert.Equal(property.Value, MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(type, property));
        Assert.Same(property, Assert.Single(MaterialShaderPropertyUtils.GetObservedProperties(type, property)));
    }

    /// <summary>Color inputs in supported shapes create parent-linked channels with opaque alpha and editable output values.</summary>
    [Theory]
    [InlineData("dictionary")]
    [InlineData("aliases")]
    [InlineData("json")]
    [InlineData("json-array")]
    [InlineData("array")]
    [InlineData("wrapped")]
    public void ColorInputsCreateLinkedChannels(string shape)
    {
        var property = MaterialShaderPropertyUtils.CreateSerializedProperty(
            new ShaderUniformInfo("tint", "vec4", ShaderUniformType.Color), CreateColorInput(shape));
        var children = Assert.IsType<Dictionary<string, SerializedProperty>>(property.Value);

        Assert.Equal(SerializedTypeEnum.Custom, property.Type);
        Assert.Equal("Color", property.SourceType);
        Assert.Equal(4, children.Count);
        Assert.All(children.Values, child => Assert.Same(property, child.ParentProperty));
        Assert.All(children.Values, child => Assert.Equal(SerializedTypeEnum.Float, child.Type));
        Assert.Equal(0.25f, children["r"].Value);
        Assert.Equal(0.5f, children["g"].Value);
        Assert.Equal(0.75f, children["b"].Value);
        Assert.Equal(1f, children["a"].Value);
        Assert.Equal(children.Values, MaterialShaderPropertyUtils.GetObservedProperties(ShaderUniformType.Color, property));

        var changes = 0;
        property.ValueChangedEvent += _ => changes++;
        children["r"].Value = 0.9f;
        var materialValue = Assert.IsType<Dictionary<string, object?>>(MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(ShaderUniformType.Color, property));
        Assert.Equal(0.9f, materialValue["r"]);
        Assert.Equal(0.5f, materialValue["g"]);
        Assert.Equal(0.75f, materialValue["b"]);
        Assert.Equal(1f, materialValue["a"]);
        Assert.Equal(1, changes);
    }

    /// <summary>Texture values accept a direct ID or nested serialized wrappers and preserve the asset reference type.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextureInputsPreserveAssetReferences(bool wrapped)
    {
        object input = wrapped ? JObject.Parse("""{"Value":{"Id":{"Value":"texture-id"}}}""") : "texture-id";
        var property = MaterialShaderPropertyUtils.CreateSerializedProperty(new ShaderUniformInfo("albedo", "sampler2D", ShaderUniformType.Texture), input);
        var child = Assert.Single(MaterialShaderPropertyUtils.GetObservedProperties(ShaderUniformType.Texture, property));

        Assert.Equal("AssetRef<Texture>", property.SourceType);
        Assert.Equal(SerializedTypeEnum.Custom, property.Type);
        Assert.Equal("Texture", property.TemplateTypeName);
        Assert.Equal("Id", child.Name);
        Assert.Equal(SerializedTypeEnum.String, child.Type);
        Assert.Equal("texture-id", child.Value);
        Assert.Same(property, child.ParentProperty);
        var output = Assert.IsType<Dictionary<string, object?>>(MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(ShaderUniformType.Texture, property));
        Assert.Equal("texture-id", output["Id"]);
    }

    /// <summary>Unsupported uniform construction fails explicitly while unsupported export returns no value.</summary>
    [Fact]
    public void UnsupportedUniformsHaveNoEditableRepresentation()
    {
        var uniform = new ShaderUniformInfo("matrix", "mat4", ShaderUniformType.Unsupported);
        Assert.Throws<ArgumentOutOfRangeException>(() => MaterialShaderPropertyUtils.CreateSerializedProperty(uniform, null));
        var property = new SerializedProperty("x", SerializedTypeEnum.Float, 1f, "float", null);
        Assert.Null(MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(ShaderUniformType.Unsupported, property));
        Assert.Empty(MaterialShaderPropertyUtils.GetObservedProperties(ShaderUniformType.Color, property));
    }

    private static object CreateColorInput(string shape)
    {
        return shape switch
        {
            "dictionary" => new Dictionary<string, object?> { ["R"] = 0.25f, ["g"] = 0.5, ["b"] = "0.75" },
            "aliases" => new Dictionary<string, object?> { ["x"] = 0.25f, ["Y"] = 0.5f, ["z"] = 0.75f },
            "json" => JObject.Parse("""{"r":0.25,"g":0.5,"b":0.75}"""),
            "json-array" => JArray.Parse("[0.25,0.5,0.75]"),
            "array" => new[] { 0.25f, 0.5f, 0.75f },
            "wrapped" => JObject.Parse("""{"Value":{"Value":{"r":{"Value":0.25},"g":0.5,"b":0.75}}}"""),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
    }
}
