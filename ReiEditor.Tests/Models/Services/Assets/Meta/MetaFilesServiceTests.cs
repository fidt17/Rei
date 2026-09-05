using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Meta;

/// <summary>
/// Verifies metadata creation, regeneration, movement, and invalid-file cleanup in an isolated project.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class MetaFilesServiceTests
{
    /// <summary>
    /// Creation writes readable metadata containing requested asset identity.
    /// </summary>
    [Fact]
    public async Task CreateMetaFileWritesReadableMetadata()
    {
        using var project = new TemporaryProjectFixture();
        var assetPath = project.Directory.GetPath("Texture.png");
        await File.WriteAllBytesAsync(assetPath, [1, 2, 3]);
        var service = CreateService(project);

        var result = await service.CreateMetaFile(new AssetMeta("asset-id"), assetPath);

        Assert.Equal(assetPath + ".meta", result.FullPath);
        Assert.Equal("asset-id", result.Object.AssetId);
        Assert.Equal("asset-id", (await project.Resources.Load<AssetMeta>(result.FullPath)).AssetId);
    }

    /// <summary>
    /// Regeneration replaces asset identity while preserving custom metadata and applying behaviour identity policy.
    /// </summary>
    [Fact]
    public async Task RegenerateMetaFilePreservesDataAndChangesIdentities()
    {
        using var project = new TemporaryProjectFixture();
        var assetPath = project.Directory.GetPath("Player.h");
        await File.WriteAllTextAsync(assetPath, "class Player {};");
        var service = CreateService(project);
        var oldMeta = new AssetMeta("old-id");
        oldMeta.AddData("Custom", "kept");
        oldMeta.AddData(BehaviourMeta.Key, new BehaviourMeta(7));
        await service.CreateMetaFile(oldMeta, assetPath);

        await service.RegenerateMetaFileForAsset(assetPath, new BehaviourMetaFileRegenerationPolicy(() => 42));

        var regenerated = await project.Resources.Load<AssetMeta>(assetPath + ".meta");
        Assert.NotEqual("old-id", regenerated.AssetId);
        Assert.Equal("kept", regenerated.GetData<string>("Custom"));
        Assert.Equal(42, regenerated.GetData<BehaviourMeta>(BehaviourMeta.Key)!.BehaviourId);
    }

    /// <summary>
    /// Moving metadata replaces stale destination metadata and retains source identity.
    /// </summary>
    [Fact]
    public async Task MoveMetaFileReplacesDestinationAndPreservesIdentity()
    {
        using var project = new TemporaryProjectFixture();
        var oldAssetPath = project.Directory.GetPath("Old.scene");
        var newAssetPath = project.Directory.GetPath("New.scene");
        var service = CreateService(project);
        await service.CreateMetaFile(new AssetMeta("source-id"), oldAssetPath);
        await service.CreateMetaFile(new AssetMeta("stale-id"), newAssetPath);

        service.MoveMetaFile(oldAssetPath, newAssetPath);

        Assert.False(File.Exists(oldAssetPath + ".meta"));
        Assert.Equal("source-id", (await project.Resources.Load<AssetMeta>(newAssetPath + ".meta")).AssetId);
    }

    /// <summary>
    /// Cleanup removes orphaned and malformed metadata while retaining metadata for an existing asset.
    /// </summary>
    [Fact]
    public async Task DeleteInvalidMetaFilesRemovesOnlyInvalidMetadata()
    {
        using var project = new TemporaryProjectFixture();
        var projectDirectory = project.Resources.GetProjectPath();
        Directory.CreateDirectory(projectDirectory);
        var validAssetPath = project.Resources.GetProjectPath("Valid.scene");
        var orphanAssetPath = project.Resources.GetProjectPath("Orphan.scene");
        var malformedAssetPath = project.Resources.GetProjectPath("Malformed.scene");
        await File.WriteAllTextAsync(validAssetPath, "{}");
        await File.WriteAllTextAsync(malformedAssetPath, "{}");
        var service = CreateService(project);
        await service.CreateMetaFile(new AssetMeta("valid-id"), validAssetPath);
        await File.WriteAllTextAsync(orphanAssetPath + ".meta", "{}");
        await File.WriteAllTextAsync(malformedAssetPath + ".meta", "{broken");

        await service.DeleteInvalidMetaFiles();

        Assert.True(File.Exists(validAssetPath + ".meta"));
        Assert.False(File.Exists(orphanAssetPath + ".meta"));
        Assert.False(File.Exists(malformedAssetPath + ".meta"));
    }

    /// <summary>
    /// Creates service with production serializer and isolated resources.
    /// </summary>
    private static MetaFilesService CreateService(TemporaryProjectFixture project)
    {
        return new MetaFilesService(project.Resources, new JsonSerializer(), new TestLogger<MetaFilesService>());
    }
}
