using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Tests.Models.Services.Assets.Shaders;

/// <summary>
/// Verifies the supported shader declaration grammar and preservation of unsupported uniforms.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Shaders")]
public sealed class ShaderUniformParserTests
{
    /// <summary>
    /// Supported scalar, color, and texture declarations preserve names and original source type text.
    /// </summary>
    [Theory]
    [InlineData("uniform float exposure;", "exposure", "float", ShaderUniformType.Float)]
    [InlineData("uniform lowp int count = 3;", "count", "int", ShaderUniformType.Integer)]
    [InlineData("uniform\nhighp vec4 tint = vec4(1.0);", "tint", "vec4", ShaderUniformType.Color)]
    [InlineData("uniform mediump sampler2D textures[4];", "textures[4]", "sampler2D", ShaderUniformType.Texture)]
    [InlineData("uniform FLOAT exposure;", "exposure", "FLOAT", ShaderUniformType.Float)]
    [InlineData("uniform mat4 transform;", "transform", "mat4", ShaderUniformType.Unsupported)]
    public void ParsesDeclaration(string source, string name, string sourceType, ShaderUniformType type)
    {
        var uniform = Assert.Single(new ShaderUniformParser().ParseUniforms(source));

        Assert.Equal(name, uniform.Name);
        Assert.Equal(sourceType, uniform.SourceType);
        Assert.Equal(type, uniform.Type);
        Assert.Equal(type != ShaderUniformType.Unsupported, uniform.IsSupported);
    }

    /// <summary>
    /// Separate block and line comments cannot hide intervening declarations or expose commented declarations.
    /// </summary>
    [Fact]
    public void RemovesCommentsAndPreservesDeclarationOrderAndDuplicates()
    {
        const string SOURCE = """
            /* uniform int hidden; */ uniform float value;
            /* second comment */ uniform int count; // uniform vec4 hiddenToo;
            uniform float value;
            """;

        var uniforms = new ShaderUniformParser().ParseUniforms(SOURCE);

        Assert.Equal(new[] { "value", "count", "value" }, uniforms.Select(uniform => uniform.Name));
        Assert.Equal(new[] { ShaderUniformType.Float, ShaderUniformType.Integer, ShaderUniformType.Float }, uniforms.Select(uniform => uniform.Type));
    }

    /// <summary>
    /// Empty, comment-only, and incomplete declarations do not create uniforms.
    /// </summary>
    [Theory]
    [InlineData(" \r\n ")]
    [InlineData("/* uniform float hidden; */ // uniform int hiddenToo;")]
    [InlineData("uniform float missingSemicolon")]
    [InlineData("uniform float 1invalid; uniform vec4; uniform int values[];")]
    public void IgnoresEmptyOrMalformedDeclarations(string source)
    {
        Assert.Empty(new ShaderUniformParser().ParseUniforms(source));
    }
}
