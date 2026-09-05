using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting.Serialization;

/// <summary>
/// Verifies namespace removal and balanced single-argument template parsing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Serialization")]
public sealed class SerializedTypeNameParserTests
{
    /// <summary>
    /// Normalization strips namespaces and whitespace while retaining nested template structure.
    /// </summary>
    [Theory]
    [InlineData(" Rei::Texture ", "Texture", "Texture", null)]
    [InlineData(" std::vector< Rei::AssetRef< Graphics::Texture > > ", "vector<AssetRef<Texture>>", "vector", "AssetRef<Texture>")]
    [InlineData("std::vector< int >", "vector<int>", "vector", "int")]
    [InlineData("", "", "", null)]
    [InlineData(" std::vector< Rei::Texture ", "vector", "vector", null)]
    [InlineData("std::vector<Rei::AssetRef<Texture>", "vector", "vector", null)]
    public void ParsesNormalizedBaseAndTemplateNames(string source, string normalized, string baseName, string? template)
    {
        Assert.Equal(normalized, SerializedTypeNameParser.NormalizeSourceType(source));
        Assert.Equal(baseName, SerializedTypeNameParser.GetBaseTypeName(source));
        Assert.Equal(template, SerializedTypeNameParser.GetTemplateTypeName(source));
    }
}
