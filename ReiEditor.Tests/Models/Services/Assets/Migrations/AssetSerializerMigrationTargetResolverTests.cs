using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Migrations;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Tests.Models.Services.Assets.Migrations;

/// <summary>
/// Verifies filename-based migration target resolution without touching the filesystem.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Migrations")]
public sealed class AssetSerializerMigrationTargetResolverTests
{
    /// <summary>
    /// Scene and material extensions and the reserved build configuration filename are matched case-insensitively.
    /// </summary>
    [Theory]
    [InlineData("Assets/Level.SCENE", typeof(Scene))]
    [InlineData("Assets/Surface.MAT", typeof(Material))]
    [InlineData("Config/build scenes configuration.ASSET", typeof(BuildScenesConfiguration))]
    public void ResolvesSupportedTargets(string path, Type expectedType)
    {
        Assert.True(AssetSerializerMigrationTargetResolver.TryResolveAssetType(path, out var assetType));
        Assert.Equal(expectedType, assetType);
    }

    /// <summary>
    /// Shader files, unrelated assets, missing extensions, and blank paths do not resolve to JSON migration targets.
    /// </summary>
    [Theory]
    [InlineData("Assets/Shader.rshader")]
    [InlineData("Config/Other.asset")]
    [InlineData("Config/Build Scenes Configuration Copy.asset")]
    [InlineData("Assets/Level")]
    [InlineData(" ")]
    public void RejectsUnsupportedTargets(string path)
    {
        Assert.False(AssetSerializerMigrationTargetResolver.TryResolveAssetType(path, out var assetType));
        Assert.Equal(typeof(Asset), assetType);
    }
}
