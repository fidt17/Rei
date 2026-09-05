using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Tests.Models.Services.Components;

/// <summary>
/// Verifies persisted property records, collection formats, and raw tokens retained for component hydration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Serialization")]
public sealed class SerializedPropertyJsonConverterTests
{
    /// <summary>
    /// Asset references, component references, and enums preserve their persisted metadata and scalar representation.
    /// </summary>
    [Theory]
    [InlineData(SerializedTypeEnum.String, "AssetRef<Texture>", "\"texture-123\"", "Texture")]
    [InlineData(SerializedTypeEnum.Integer, "ComponentRef<Camera>", "17", "Camera")]
    [InlineData(SerializedTypeEnum.Enum, "BlendMode", "2", null)]
    public void ScalarReferenceRecordsReadAndWriteExpectedFormat(SerializedTypeEnum type, string sourceType, string valueJson, string? templateType)
    {
        var expected = JObject.Parse($$"""{"Name":"target","Type":{{(int)type}},"SourceType":"{{sourceType}}","Value":{{valueJson}}} """);
        object value = type == SerializedTypeEnum.String ? "texture-123" : type == SerializedTypeEnum.Enum ? 2 : 17;
        var original = new SerializedProperty("target", type, value, sourceType, null);

        var written = JObject.Parse(JsonConvert.SerializeObject(original));
        var loaded = JsonConvert.DeserializeObject<SerializedProperty>(expected.ToString())!;

        Assert.True(JToken.DeepEquals(expected, written), written.ToString());
        Assert.Equal("target", loaded.Name);
        Assert.Equal(type, loaded.Type);
        Assert.Equal(sourceType, loaded.SourceType);
        Assert.Equal(templateType, loaded.TemplateTypeName);
        Assert.Null(loaded.ParentProperty);
        if (type == SerializedTypeEnum.String) Assert.Equal("texture-123", Assert.IsType<string>(loaded.Value));
        else Assert.Equal(type == SerializedTypeEnum.Enum ? 2L : 17L, Assert.IsType<long>(loaded.Value));
    }

    /// <summary>
    /// Custom values serialize child property records keyed by child names while collections serialize bare values.
    /// </summary>
    [Fact]
    public void SerializeNestedCustomAndCollectionsMatchesIndependentDocument()
    {
        var position = new SerializedProperty("position", SerializedTypeEnum.Custom, null, "Vector", null);
        position.Value = new Dictionary<string, SerializedProperty>
        {
            ["storage-key"] = new("x", SerializedTypeEnum.Float, 1.25, "float", position)
        };
        var nested = new SerializedProperty("ignored-index", SerializedTypeEnum.Collection,
            new List<SerializedProperty> { new("0", SerializedTypeEnum.Integer, 3, "int", null) }, "vector<int>", null);
        var root = new SerializedProperty("items", SerializedTypeEnum.Collection,
            new List<SerializedProperty> { position, nested }, "vector<Mixed>", null);
        var expected = JToken.Parse("""
            {"Name":"items","Type":7,"SourceType":"vector<Mixed>","Value":[
                {"x":{"Name":"x","Type":4,"SourceType":"float","Value":1.25}},
                [3]
            ]}
            """);

        var actual = JToken.Parse(JsonConvert.SerializeObject(root));

        Assert.True(JToken.DeepEquals(expected, actual), actual.ToString());
    }

    /// <summary>
    /// Modern bare items and legacy wrapped items normalize to identical raw arrays with collection type metadata.
    /// </summary>
    [Theory]
    [InlineData("collection-modern.json")]
    [InlineData("collection-legacy.json")]
    public void DeserializeCollectionFixtureNormalizesLegacyWrappers(string fixtureName)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Serialization", fixtureName));

        var property = JsonConvert.DeserializeObject<SerializedProperty>(source)!;

        var array = Assert.IsType<JArray>(property.Value);
        Assert.True(JToken.DeepEquals(JArray.Parse("""["texture-id",null,{"Type":2,"Value":17},[1,2]]"""), array), array.ToString());
        Assert.Equal("textures", property.Name);
        Assert.Equal(SerializedTypeEnum.Collection, property.Type);
        Assert.Equal("std::vector<AssetRef<Texture>>", property.SourceType);
        Assert.Equal("AssetRef<Texture>", property.TemplateTypeName);
        Assert.Equal("AssetRef<Texture>", property.ItemSourceType);
        Assert.Equal("Texture", property.ItemTemplateTypeName);
        Assert.Equal(SerializedTypeEnum.Invalid, property.ItemType);
    }

    /// <summary>
    /// Custom records retain their nested JObject data until component refresh hydrates the property tree.
    /// </summary>
    [Fact]
    public void DeserializeCustomRetainsNestedPropertyRecordsAsTokens()
    {
        const string SOURCE = """{"Name":"position","Type":5,"SourceType":"Vector","Value":{"x":{"Name":"x","Type":4,"SourceType":"float","Value":2.5}}}""";

        var property = JsonConvert.DeserializeObject<SerializedProperty>(SOURCE)!;

        Assert.Equal("position", property.Name);
        Assert.Equal(SerializedTypeEnum.Custom, property.Type);
        var value = Assert.IsType<JObject>(property.Value);
        Assert.True(JToken.DeepEquals(JObject.Parse("""{"x":{"Name":"x","Type":4,"SourceType":"float","Value":2.5}}"""), value));
    }

    /// <summary>
    /// Omitted fields use empty metadata, Invalid type, and a null value without fabricating child properties.
    /// </summary>
    [Fact]
    public void DeserializeMissingFieldsUsesEmptyMetadata()
    {
        var property = JsonConvert.DeserializeObject<SerializedProperty>("{}")!;

        Assert.Equal(string.Empty, property.Name);
        Assert.Equal(string.Empty, property.SourceType);
        Assert.Equal(SerializedTypeEnum.Invalid, property.Type);
        Assert.Null(property.Value);
        Assert.Null(property.TemplateTypeName);
        Assert.Null(property.ParentProperty);
    }

    /// <summary>
    /// Scalar and array top-level tokens are rejected because persisted properties must be objects.
    /// </summary>
    [Theory]
    [InlineData("42")]
    [InlineData("[]")]
    public void DeserializeNonObjectThrows(string source)
    {
        var error = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<SerializedProperty>(source));

        Assert.Contains("Expected serialized property object", error.Message);
    }

    /// <summary>
    /// The converter accepts and writes a null property independently of the editor serializer's required-value policy.
    /// </summary>
    [Fact]
    public void ConverterReadsAndWritesNullProperty()
    {
        var converter = new SerializedPropertyJsonConverter();
        var serializer = Newtonsoft.Json.JsonSerializer.CreateDefault();
        using var input = new StringReader("null");
        using var reader = new JsonTextReader(input);
        Assert.True(reader.Read());
        using var output = new StringWriter();
        using var writer = new JsonTextWriter(output);

        Assert.Null(converter.ReadJson(reader, typeof(SerializedProperty), null, false, serializer));
        converter.WriteJson(writer, null, serializer);
        writer.Flush();

        Assert.Equal("null", output.ToString());
    }
}
