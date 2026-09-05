using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets;

/// <summary>Verifies template type mapping, engine asset IDs, and runtime asset type resolution.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "AssetTypes")]
public sealed class AssetTypeUtilityTests
{
    /// <summary>Supported template aliases map case-insensitively, while blank and unknown names have no asset type.</summary>
    [Theory]
    [InlineData("MODEL", AssetType.Model)]
    [InlineData("Material", AssetType.Material)]
    [InlineData("texture", AssetType.Texture)]
    [InlineData("Font", AssetType.Font)]
    [InlineData("render::Font", AssetType.Font)]
    [InlineData("rei::render::font", AssetType.Font)]
    [InlineData("Unknown", AssetType.Unknown)]
    [InlineData(null, AssetType.Unknown)]
    public void TemplateAliasesResolveToAssetTypes(string? template, AssetType expected)
    {
        Assert.Equal(expected, new AssetTypeMapper().GetAssetTypeForTemplateType(template));
    }

    /// <summary>Extension lists expose the supported file formats without accepting unknown asset types.</summary>
    [Fact]
    public void AssetTypesExposeTheirSupportedExtensions()
    {
        var mapper = new AssetTypeMapper();
        Assert.Equal(new[] { ".obj", ".fbx" }, mapper.GetExtensionsForAssetType(AssetType.Model));
        Assert.Equal(new[] { ".mat" }, mapper.GetExtensionsForAssetType(AssetType.Material));
        Assert.Equal(new[] { ".png", ".jpg" }, mapper.GetExtensionsForAssetType(AssetType.Texture));
        Assert.Equal(new[] { ".ttf", ".otf" }, mapper.GetExtensionsForAssetType(AssetType.Font));
        Assert.Empty(mapper.GetExtensionsForAssetType(AssetType.Unknown));
    }

    /// <summary>Engine IDs use normalized filenames only for assets under the engine resource root.</summary>
    [Fact]
    public void EngineAssetIdsAreStableAndRejectOutsidePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReiTypes", "Engine Resources");
        Assert.True(ReiAssetIdUtility.TryCreateFromAssetPath(Path.Combine(root, "Textures", "White.PNG"), root, out var id));
        Assert.Equal("rei_white.png", id);
        Assert.False(ReiAssetIdUtility.TryCreateFromAssetPath(Path.Combine(root + "Other", "White.PNG"), root, out var outsideId));
        Assert.Equal("", outsideId);
        Assert.False(ReiAssetIdUtility.TryCreateFromAssetPath(" ", root, out _));
        Assert.False(ReiAssetIdUtility.TryCreateFromAssetPath(Path.Combine(root, "White.PNG"), "", out _));
    }

    /// <summary>Only registered material files expose a runtime type; unknown IDs and other asset formats fail cleanly.</summary>
    [Fact]
    public void RuntimeTypeResolutionRequiresRegisteredMaterial()
    {
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets(new[]
        {
            new AssetInfo(new AssetMeta("material"), Path.Combine(Path.GetTempPath(), "surface.MAT")),
            new AssetInfo(new AssetMeta("texture"), Path.Combine(Path.GetTempPath(), "image.png"))
        });

        Assert.True(RuntimeAssetTypeResolver.TryResolveAssetType(registry, "material", out var type));
        Assert.Equal("Material", type);
        foreach (var id in new[] { "texture", "missing", "" })
        {
            Assert.False(RuntimeAssetTypeResolver.TryResolveAssetType(registry, id, out var unsupported));
            Assert.Equal("", unsupported);
        }
    }
}
