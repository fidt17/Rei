using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Resources;
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
/// Verifies asset operation edge contracts around paths, metadata, registry state, and partial failures.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class AssetOperationsEdgeCaseTests
{
    /// <summary>
    /// Captures partial import requests without performing importer work.
    /// </summary>
    private sealed class TestAssetImporter : IAssetImporter
    {
        public event Action ImportedAssetsEvent = delegate { };
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting { get; } = new Observable<bool>(false);
        public List<IReadOnlyList<string>> Requests { get; } = new();

        /// <summary>Rejects unused full imports.</summary>
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();

        /// <summary>Captures one partial import request.</summary>
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths)
        {
            Requests.Add(paths.ToArray());
            return Task.FromResult(new List<AssetInfo>());
        }
    }

    /// <summary>
    /// Supplies deterministic behaviour IDs when a regeneration policy requests one.
    /// </summary>
    private sealed class TestBehaviourRegistry : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>();

        /// <summary>Rejects unused ID lookup.</summary>
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => throw new NotSupportedException();

        /// <summary>Rejects unused name lookup.</summary>
        public int? GetIdByName(string name) => throw new NotSupportedException();

        /// <summary>Returns deterministic new behaviour identity.</summary>
        public int AllocateBehaviourId() => 101;

        /// <summary>Rejects unused refresh.</summary>
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    /// <summary>
    /// Classifies every test path as a regular asset.
    /// </summary>
    private sealed class TestBehaviourFileUtility : IBehaviourFileUtility
    {
        /// <summary>Rejects unused behaviour enumeration.</summary>
        public List<BehaviourFileUtility.BehaviourPathData> GetAllBehaviours() => throw new NotSupportedException();

        /// <summary>Rejects unused metadata enumeration.</summary>
        public Task<List<ObjectFile<AssetMeta>>> GetAllBehaviourMetas() => throw new NotSupportedException();

        /// <summary>Rejects unused source parsing.</summary>
        public bool TryGetBehaviourNameFrom(string text, out string name) => throw new NotSupportedException();

        /// <summary>Classifies path as a non-behaviour asset.</summary>
        public Task<bool> IsBehaviourFile(string path) => Task.FromResult(false);
    }

    /// <summary>
    /// Throws during bulk regeneration to expose operation failure boundaries.
    /// </summary>
    private sealed class TestThrowingMetaFilesService : IMetaFilesService
    {
        public IOException Failure { get; } = new("regeneration failed");
        public IReadOnlyList<string>? RegenerationTargets { get; private set; }

        /// <summary>Rejects unused metadata creation.</summary>
        public Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string assetPath) => throw new NotSupportedException();

        /// <summary>Captures targets and fails after external files have been copied.</summary>
        public Task RegenerateMetaFilesForTargets(IEnumerable<string> targets, IMetaFileRegenerationPolicy policy)
        {
            RegenerationTargets = targets.ToArray();
            throw Failure;
        }

        /// <summary>Rejects unused directory regeneration.</summary>
        public Task RegenerateMetaFilesInDirectory(string directoryPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused asset regeneration.</summary>
        public Task RegenerateMetaFileForAsset(string assetPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused metadata movement.</summary>
        public void MoveMetaFile(string oldAssetPath, string newAssetPath) => throw new NotSupportedException();

        /// <summary>Rejects unused metadata deletion.</summary>
        public void DeleteMetaFile(string assetPath) => throw new NotSupportedException();

        /// <summary>Rejects unused invalid metadata cleanup.</summary>
        public Task DeleteInvalidMetaFiles() => throw new NotSupportedException();
    }

    /// <summary>
    /// Renaming a directory moves its tree and updates every nested registry path.
    /// </summary>
    [Fact]
    public async Task RenameDirectoryMovesTreeAndNestedRegistryPaths()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("OldFolder");
        var nested = Path.Combine(source, "Nested");
        Directory.CreateDirectory(nested);
        var sourceAsset = Path.Combine(nested, "Asset.scene");
        await File.WriteAllTextAsync(sourceAsset, "scene");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([new AssetInfo(new AssetMeta("nested-id"), sourceAsset)]);
        var service = CreateService(project, registry, CreateMetaService(project), out _);
        var expectedDirectory = project.Directory.GetPath("NewFolder");
        var expectedAsset = Path.Combine(expectedDirectory, "Nested", "Asset.scene");

        await service.RenameAsync(source, " NewFolder ");

        Assert.False(Directory.Exists(source));
        Assert.Equal("scene", await File.ReadAllTextAsync(expectedAsset));
        Assert.True(registry.TryGetById("nested-id", out var registered));
        Assert.Equal(expectedAsset, registered.FullPath);
    }

    /// <summary>
    /// Invalid rename inputs leave an existing asset and registry identity unchanged.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Asset.scene")]
    public async Task RenameInvalidOrSameNameLeavesAssetUntouched(string newName)
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Asset.scene");
        await File.WriteAllTextAsync(source, "scene");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([new AssetInfo(new AssetMeta("asset-id"), source)]);
        var service = CreateService(project, registry, CreateMetaService(project), out _);

        await service.RenameAsync(source, newName);

        Assert.Equal("scene", await File.ReadAllTextAsync(source));
        Assert.True(registry.TryGetById("asset-id", out var registered));
        Assert.Equal(source, registered.FullPath);
    }

    /// <summary>
    /// Deleting a file removes its sidecar metadata and registry entry.
    /// </summary>
    [Fact]
    public async Task DeleteFileRemovesMetadataAndRegistryEntry()
    {
        using var project = new TemporaryProjectFixture();
        var asset = project.Directory.GetPath("Asset.scene");
        await File.WriteAllTextAsync(asset, "scene");
        var metaService = CreateMetaService(project);
        var meta = new AssetMeta("asset-id");
        await metaService.CreateMetaFile(meta, asset);
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([new AssetInfo(meta, asset)]);
        var service = CreateService(project, registry, metaService, out _);

        await service.DeleteAsync(asset, false);

        Assert.False(File.Exists(asset));
        Assert.False(File.Exists(asset + ".meta"));
        Assert.False(registry.TryGetById("asset-id", out _));
    }

    /// <summary>
    /// Moving a file selects a unique destination and carries metadata and registry identity with it.
    /// </summary>
    [Fact]
    public async Task MoveFileUsesUniquePathAndPreservesMetadataIdentity()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Asset.scene");
        var destination = project.Directory.GetPath("Destination");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(source, "source");
        await File.WriteAllTextAsync(Path.Combine(destination, "Asset.scene"), "collision");
        var metaService = CreateMetaService(project);
        var meta = new AssetMeta("asset-id");
        await metaService.CreateMetaFile(meta, source);
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([new AssetInfo(meta, source)]);
        var service = CreateService(project, registry, metaService, out _);
        var expected = Path.Combine(destination, "Asset 2.scene");

        await service.MoveAsync(source, destination);

        Assert.False(File.Exists(source));
        Assert.False(File.Exists(source + ".meta"));
        Assert.Equal("source", await File.ReadAllTextAsync(expected));
        Assert.Equal("asset-id", (await project.Resources.Load<AssetMeta>(expected + ".meta")).AssetId);
        Assert.True(registry.TryGetById("asset-id", out var registered));
        Assert.Equal(expected, registered.FullPath);
    }

    /// <summary>
    /// Moving a directory selects a unique destination and updates nested registry paths.
    /// </summary>
    [Fact]
    public async Task MoveDirectoryUsesUniquePathAndUpdatesNestedRegistryPaths()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Folder");
        var destination = project.Directory.GetPath("Destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(Path.Combine(destination, "Folder"));
        var sourceAsset = Path.Combine(source, "Asset.scene");
        await File.WriteAllTextAsync(sourceAsset, "scene");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([new AssetInfo(new AssetMeta("asset-id"), sourceAsset)]);
        var service = CreateService(project, registry, CreateMetaService(project), out _);
        var expected = Path.Combine(destination, "Folder 2", "Asset.scene");

        await service.MoveAsync(source, destination);

        Assert.False(Directory.Exists(source));
        Assert.Equal("scene", await File.ReadAllTextAsync(expected));
        Assert.True(registry.TryGetById("asset-id", out var registered));
        Assert.Equal(expected, registered.FullPath);
    }

    /// <summary>
    /// Moving to a missing or blank destination leaves source and metadata untouched.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveInvalidDestinationLeavesSourceUntouched(bool useBlankDestination)
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Asset.scene");
        await File.WriteAllTextAsync(source, "scene");
        var metaService = CreateMetaService(project);
        await metaService.CreateMetaFile(new AssetMeta("asset-id"), source);
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), metaService, out _);
        var destination = useBlankDestination ? " " : project.Directory.GetPath("Missing");

        await service.MoveAsync(source, destination);

        Assert.Equal("scene", await File.ReadAllTextAsync(source));
        Assert.True(File.Exists(source + ".meta"));
    }

    /// <summary>
    /// Duplicating a directory regenerates independent metadata throughout its copied tree and reimports its root.
    /// </summary>
    [Fact]
    public async Task DuplicateDirectoryRegeneratesNestedMetadataAndReimportsRoot()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Folder");
        var nested = Path.Combine(source, "Nested");
        Directory.CreateDirectory(nested);
        var first = Path.Combine(source, "First.scene");
        var second = Path.Combine(nested, "Second.mat");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(second, "second");
        var metaService = CreateMetaService(project);
        await metaService.CreateMetaFile(new AssetMeta("first-id"), first);
        await metaService.CreateMetaFile(new AssetMeta("second-id"), second);
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), metaService, out var importer);
        var copy = project.Directory.GetPath("Folder Copy");
        var copiedFirst = Path.Combine(copy, "First.scene");
        var copiedSecond = Path.Combine(copy, "Nested", "Second.mat");

        await service.DuplicateAsync(source, true);

        Assert.Equal("first", await File.ReadAllTextAsync(copiedFirst));
        Assert.Equal("second", await File.ReadAllTextAsync(copiedSecond));
        Assert.NotEqual("first-id", (await project.Resources.Load<AssetMeta>(copiedFirst + ".meta")).AssetId);
        Assert.NotEqual("second-id", (await project.Resources.Load<AssetMeta>(copiedSecond + ".meta")).AssetId);
        Assert.Equal([copy], Assert.Single(importer.Requests));
    }

    /// <summary>
    /// Duplicating a missing file contains the filesystem failure and issues no import request.
    /// </summary>
    [Fact]
    public async Task DuplicateMissingFileDoesNotRequestImport()
    {
        using var project = new TemporaryProjectFixture();
        var missing = project.Directory.GetPath("Missing.scene");
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), CreateMetaService(project), out var importer);

        await service.DuplicateAsync(missing, false);

        Assert.Empty(importer.Requests);
        Assert.False(File.Exists(project.Directory.GetPath("Missing Copy.scene")));
    }

    /// <summary>
    /// External import copies files and directories to unique paths, ignores invalid inputs, regenerates metadata, and reimports created roots once.
    /// </summary>
    [Fact]
    public async Task ImportExternalAssetsHandlesMixedInputsAndUniqueDestinations()
    {
        using var project = new TemporaryProjectFixture();
        var sourceFile = project.Directory.GetPath("Source", "Asset.scene");
        var sourceDirectory = project.Directory.GetPath("Source", "Pack");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(sourceFile, "file");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "Nested.mat"), "nested");
        var target = project.Directory.GetPath("Target");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "Asset.scene"), "collision");
        Directory.CreateDirectory(Path.Combine(target, "Pack"));
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), CreateMetaService(project), out var importer);
        var importedFile = Path.Combine(target, "Asset 2.scene");
        var importedDirectory = Path.Combine(target, "Pack 2");
        var importedNested = Path.Combine(importedDirectory, "Nested.mat");

        await service.ImportExternalAssets([" ", project.Directory.GetPath("Missing.scene"), sourceFile, sourceDirectory], target);

        Assert.Equal("file", await File.ReadAllTextAsync(importedFile));
        Assert.Equal("nested", await File.ReadAllTextAsync(importedNested));
        Assert.True(File.Exists(importedFile + ".meta"));
        Assert.True(File.Exists(importedNested + ".meta"));
        Assert.Equal([importedFile, importedDirectory], Assert.Single(importer.Requests));
    }

    /// <summary>
    /// External import with an invalid target does not copy files or request reimport.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportExternalAssetsInvalidTargetDoesNothing(bool useBlankTarget)
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Source.scene");
        await File.WriteAllTextAsync(source, "scene");
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), CreateMetaService(project), out var importer);
        var target = useBlankTarget ? " " : project.Directory.GetPath("MissingTarget");

        await service.ImportExternalAssets([source], target);

        Assert.Empty(importer.Requests);
        Assert.Equal("scene", await File.ReadAllTextAsync(source));
    }

    /// <summary>
    /// Metadata regeneration failure leaves copied external files visible but prevents an import request.
    /// </summary>
    [Fact]
    public async Task ImportExternalAssetsRegenerationFailureStopsBeforeReimport()
    {
        using var project = new TemporaryProjectFixture();
        var source = project.Directory.GetPath("Source", "Asset.scene");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await File.WriteAllTextAsync(source, "scene");
        var target = project.Directory.GetPath("Target");
        Directory.CreateDirectory(target);
        var metaService = new TestThrowingMetaFilesService();
        var logger = new TestLogger<AssetOperationsService>();
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), metaService, out var importer, logger);
        var copied = Path.Combine(target, "Asset.scene");

        await service.ImportExternalAssets([source], target);

        Assert.Equal("scene", await File.ReadAllTextAsync(copied));
        var regenerationTargets = Assert.IsAssignableFrom<IReadOnlyList<string>>(metaService.RegenerationTargets);
        Assert.Equal([copied], regenerationTargets);
        Assert.Same(metaService.Failure, Assert.Single(logger.Entries).Exception);
        Assert.Empty(importer.Requests);
    }

    /// <summary>
    /// Folder creation selects the next unique name without modifying existing folders.
    /// </summary>
    [Fact]
    public async Task CreateFolderUsesUniqueName()
    {
        using var project = new TemporaryProjectFixture();
        var existing = project.Directory.GetPath("New Folder");
        Directory.CreateDirectory(existing);
        await File.WriteAllTextAsync(Path.Combine(existing, "marker.txt"), "keep");
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), CreateMetaService(project), out _);

        await service.CreateFolderAsync(project.Directory.RootPath, "New Folder");

        Assert.True(Directory.Exists(project.Directory.GetPath("New Folder 2")));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(existing, "marker.txt")));
    }

    /// <summary>
    /// Folder creation rejects blank names and missing parents without creating filesystem entries.
    /// </summary>
    [Fact]
    public async Task CreateFolderInvalidInputsDoNothing()
    {
        using var project = new TemporaryProjectFixture();
        var service = CreateService(project, new AssetRegistry(new TestLogger<AssetRegistry>()), CreateMetaService(project), out _);
        var missingParent = project.Directory.GetPath("MissingParent");
        var entriesBefore = Directory.GetFileSystemEntries(project.Directory.RootPath);

        await service.CreateFolderAsync(project.Directory.RootPath, " ");
        await service.CreateFolderAsync(missingParent, "Folder");

        Assert.Equal(entriesBefore, Directory.GetFileSystemEntries(project.Directory.RootPath));
        Assert.False(Directory.Exists(missingParent));
    }

    /// <summary>
    /// Creates production metadata service over isolated temporary resources.
    /// </summary>
    private static MetaFilesService CreateMetaService(TemporaryProjectFixture project)
    {
        return new MetaFilesService(project.Resources, new JsonSerializer(), new TestLogger<MetaFilesService>());
    }

    /// <summary>
    /// Creates operation service with isolated collaborators and exposes captured import requests.
    /// </summary>
    private static AssetOperationsService CreateService(TemporaryProjectFixture project, IAssetRegistry registry, IMetaFilesService metaService, out TestAssetImporter importer, TestLogger<AssetOperationsService>? logger = null)
    {
        importer = new TestAssetImporter();
        return new AssetOperationsService(
            logger ?? new TestLogger<AssetOperationsService>(), project.Resources, importer, registry, metaService,
            new TestBehaviourRegistry(), new TestBehaviourFileUtility());
    }
}
