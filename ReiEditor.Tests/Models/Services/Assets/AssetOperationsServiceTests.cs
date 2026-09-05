using ReiEditor.Models.Resources;
using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Assets;

/// <summary>
/// Verifies asset filesystem operations keep metadata, registry state, and import requests consistent.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class AssetOperationsServiceTests
{
    /// <summary>
    /// Captures requested reimport path sets.
    /// </summary>
    private sealed class TestAssetImporter : IAssetImporter
    {
        public event Action ImportedAssetsEvent = delegate { };
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting { get; } = new Observable<bool>(false);
        public List<IReadOnlyList<string>> Requests { get; } = new();

        /// <summary>Rejects unused full import.</summary>
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();

        /// <summary>Captures partial import paths.</summary>
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths)
        {
            Requests.Add(paths.ToArray());
            return Task.FromResult(new List<AssetInfo>());
        }
    }

    /// <summary>
    /// Supplies behaviour IDs for regeneration policies.
    /// </summary>
    private sealed class TestBehaviourRegistry : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>();

        /// <summary>Rejects unused behaviour lookup.</summary>
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => throw new NotSupportedException();

        /// <summary>Rejects unused name lookup.</summary>
        public int? GetIdByName(string name) => throw new NotSupportedException();

        /// <summary>Allocates deterministic behaviour identity.</summary>
        public int AllocateBehaviourId() => 100;

        /// <summary>Rejects unused refresh.</summary>
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    /// <summary>
    /// Treats no test asset as a behaviour file.
    /// </summary>
    private sealed class TestBehaviourFileUtility : IBehaviourFileUtility
    {
        /// <summary>Rejects unused behaviour enumeration.</summary>
        public List<BehaviourFileUtility.BehaviourPathData> GetAllBehaviours() => throw new NotSupportedException();

        /// <summary>Rejects unused metadata enumeration.</summary>
        public Task<List<ObjectFile<AssetMeta>>> GetAllBehaviourMetas() => throw new NotSupportedException();

        /// <summary>Rejects unused behaviour parsing.</summary>
        public bool TryGetBehaviourNameFrom(string text, out string name) => throw new NotSupportedException();

        /// <summary>Returns false for all paths.</summary>
        public Task<bool> IsBehaviourFile(string path) => Task.FromResult(false);
    }


    /// <summary>
    /// Renaming a file moves its metadata and updates registry path without changing asset identity.
    /// </summary>
    [Fact]
    public async Task RenameFilePreservesMetadataAndRegistryIdentity()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Old.scene");
        var target = project.Directory.GetPath("New.scene");
        await File.WriteAllTextAsync(source, "scene");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        var metaService = CreateMetaService(project);
        var meta = new AssetMeta("scene-id");
        await metaService.CreateMetaFile(meta, source);
        registry.RegisterNewAssets([new AssetInfo(meta, source)]);
        var service = CreateService(project, registry, metaService, out _);

        await service.RenameAsync(source, "  New.scene  ");

        Assert.False(File.Exists(source));
        Assert.False(File.Exists(source + ".meta"));
        Assert.Equal("scene", await File.ReadAllTextAsync(target));
        Assert.Equal("scene-id", (await project.Resources.Load<AssetMeta>(target + ".meta")).AssetId);
        Assert.True(registry.TryGetById("scene-id", out var registered));
        Assert.Equal(target, registered.FullPath);
    }

    /// <summary>
    /// Duplicating a file selects next collision suffix, creates a new metadata identity, and reimports only created path.
    /// </summary>
    [Fact]
    public async Task DuplicateFileUsesUniquePathAndNewIdentity()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Asset.scene");
        var firstCollision = project.Directory.GetPath("Asset Copy.scene");
        var expected = project.Directory.GetPath("Asset Copy 1.scene");
        await File.WriteAllTextAsync(source, "source-bytes");
        await File.WriteAllTextAsync(firstCollision, "collision");
        var metaService = CreateMetaService(project);
        await metaService.CreateMetaFile(new AssetMeta("source-id"), source);
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), metaService, out var importer);

        await service.DuplicateAsync(source, false);

        Assert.Equal("source-bytes", await File.ReadAllTextAsync(expected));
        Assert.NotEqual("source-id", (await project.Resources.Load<AssetMeta>(expected + ".meta")).AssetId);
        Assert.Equal([expected], Assert.Single(importer.Requests));
    }

    /// <summary>
    /// Deleting a directory removes its subtree and registry entries while retaining adjacent prefix paths.
    /// </summary>
    [Fact]
    public async Task DeleteDirectoryRemovesOnlySubtreeRegistryEntries()
    {
        using var project = new TemporaryProjectFixture();
        var deletedDirectory = project.Directory.GetPath("Assets");
        var adjacentDirectory = project.Directory.GetPath("AssetsExtra");
        Directory.CreateDirectory(deletedDirectory);
        Directory.CreateDirectory(adjacentDirectory);
        var deletedAsset = Path.Combine(deletedDirectory, "A.scene");
        var retainedAsset = Path.Combine(adjacentDirectory, "B.scene");
        await File.WriteAllTextAsync(deletedAsset, "a");
        await File.WriteAllTextAsync(retainedAsset, "b");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([
            new AssetInfo(new AssetMeta("delete-id"), deletedAsset),
            new AssetInfo(new AssetMeta("keep-id"), retainedAsset)
        ]);
        var service = CreateService(project, registry, CreateMetaService(project), out _);

        await service.DeleteAsync(deletedDirectory, true);

        Assert.False(Directory.Exists(deletedDirectory));
        Assert.False(registry.TryGetById("delete-id", out _));
        Assert.True(registry.TryGetById("keep-id", out var retained));
        Assert.Equal(retainedAsset, retained.FullPath);
    }

    /// <summary>
    /// Rename collision is rejected without changing source files, metadata, or registry path.
    /// </summary>
    [Fact]
    public async Task RenameCollisionLeavesSourceUntouched()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Source.scene");
        var collision = project.Directory.GetPath("Taken.scene");
        await File.WriteAllTextAsync(source, "source");
        await File.WriteAllTextAsync(collision, "taken");
        var metaService = CreateMetaService(project);
        var meta = new AssetMeta("source-id");
        await metaService.CreateMetaFile(meta, source);
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([new AssetInfo(meta, source)]);
        var service = CreateService(project, registry, metaService, out _);

        await service.RenameAsync(source, "Taken.scene");

        Assert.Equal("source", await File.ReadAllTextAsync(source));
        Assert.Equal("taken", await File.ReadAllTextAsync(collision));
        Assert.True(File.Exists(source + ".meta"));
        Assert.True(registry.TryGetById("source-id", out var registered));
        Assert.Equal(source, registered.FullPath);
    }

    /// <summary>
    /// Creates production metadata service for isolated resources.
    /// </summary>
    private static MetaFilesService CreateMetaService(TemporaryProjectFixture project)
    {
        return new MetaFilesService(project.Resources, new JsonSerializer(), new TestLogger<MetaFilesService>());
    }

    /// <summary>
    /// Creates operations service and exposes importer requests.
    /// </summary>
    private static AssetOperationsService CreateService(TemporaryProjectFixture project, IAssetRegistry registry, IMetaFilesService metaService, out TestAssetImporter importer)
    {
        importer = new TestAssetImporter();
        return new AssetOperationsService(
            new TestLogger<AssetOperationsService>(), project.Resources, importer, registry, metaService,
            new TestBehaviourRegistry(), new TestBehaviourFileUtility());
    }

}
