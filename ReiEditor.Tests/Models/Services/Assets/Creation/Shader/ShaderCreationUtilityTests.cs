using ReiEditor.Models.ProjectManagement.Template;
using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation.Shader;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Assets.Creation.Shader;

/// <summary>
/// Verifies shader template persistence, metadata creation, import requests, and validation failures.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class ShaderCreationUtilityTests
{
    /// <summary>Allocates fixed shader identity.</summary>
    private sealed class TestAssetCreator : IAssetCreator
    {
        /// <summary>Returns fixed shader identity.</summary>
        public string AllocateAssetId() => "shader-id";

        /// <summary>Rejects unused create overload.</summary>
        public Task<bool> Create(Asset asset, string projectPath) => throw new NotSupportedException();

        /// <summary>Rejects unused identified create overload.</summary>
        public Task<bool> Create(Asset asset, string id, string projectPath) => throw new NotSupportedException();
    }

    /// <summary>Controls unique-name validation.</summary>
    private sealed class TestAssetRegistry : IAssetRegistry
    {
        public bool IsUnique { get; init; } = true;

        /// <summary>Rejects unused ID lookup.</summary>
        public bool TryGetById(string assetId, [NotNullWhen(true)] out AssetInfo? assetInfo) => throw new NotSupportedException();

        /// <summary>Rejects unused typed lookup.</summary>
        public bool TryGetByIdAndExtensions(string assetId, IReadOnlyCollection<string> extensions, [NotNullWhen(true)] out AssetInfo? assetInfo) => throw new NotSupportedException();

        /// <summary>Rejects unused path lookup.</summary>
        public bool TryGetByPath(string fullPath, [NotNullWhen(true)] out AssetInfo? assetInfo) => throw new NotSupportedException();

        /// <summary>Rejects unused loaded lookup.</summary>
        public bool TryGetLoadedAsset(string assetId, [NotNullWhen(true)] out Asset? asset) => throw new NotSupportedException();

        /// <summary>Rejects unused registry replacement.</summary>
        public void UpdateRegistry(IEnumerable<AssetInfo> assets) => throw new NotSupportedException();

        /// <summary>Rejects unused registration.</summary>
        public void RegisterNewAssets(IEnumerable<AssetInfo> assets) => throw new NotSupportedException();

        /// <summary>Rejects unused path update.</summary>
        public void UpdateRegistryPath(string oldPath, string newPath) => throw new NotSupportedException();

        /// <summary>Rejects unused path removal.</summary>
        public void UnregisterByPath(string fullPath) => throw new NotSupportedException();

        /// <summary>Rejects unused subtree removal.</summary>
        public void UnregisterUnderDirectory(string directoryPath) => throw new NotSupportedException();

        /// <summary>Rejects unused dirty-asset enumeration.</summary>
        public IEnumerable<Asset> GetDirtyAssets() => throw new NotSupportedException();

        /// <summary>Rejects unused loaded-info enumeration.</summary>
        public IEnumerable<AssetInfo> GetLoadedAssetInfos() => throw new NotSupportedException();

        /// <summary>Rejects unused asset enumeration.</summary>
        public IEnumerable<AssetInfo> GetAllAssets() => throw new NotSupportedException();

        /// <summary>Rejects unused extension enumeration.</summary>
        public IEnumerable<AssetInfo> GetAllAssetsByExtensions(IReadOnlyCollection<string> extensions) => throw new NotSupportedException();

        /// <summary>Returns configured uniqueness.</summary>
        public bool IsUniqueAssetName(string assetName, string assetExtension) => IsUnique;

        /// <summary>Rejects unused loaded-asset addition.</summary>
        public void AddToLoadedAssets(AssetInfo assetInfo, Asset asset) => throw new NotSupportedException();

        /// <summary>Rejects unused loaded-asset removal.</summary>
        public void RemoveFromLoadedAssets(AssetInfo assetInfo) => throw new NotSupportedException();
    }

    /// <summary>Captures created shader metadata.</summary>
    private sealed class TestMetaFilesService : IMetaFilesService
    {
        public AssetMeta? CreatedMeta { get; private set; }
        public string? CreatedPath { get; private set; }

        /// <summary>Captures metadata creation.</summary>
        public Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string assetPath) { CreatedMeta = meta; CreatedPath = assetPath; return Task.FromResult(new ObjectFile<AssetMeta>(meta, assetPath + ".meta")); }

        /// <summary>Rejects unused target regeneration.</summary>
        public Task RegenerateMetaFilesForTargets(IEnumerable<string> targets, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused directory regeneration.</summary>
        public Task RegenerateMetaFilesInDirectory(string directoryPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused asset regeneration.</summary>
        public Task RegenerateMetaFileForAsset(string assetPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused metadata move.</summary>
        public void MoveMetaFile(string oldAssetPath, string newAssetPath) => throw new NotSupportedException();

        /// <summary>Rejects unused metadata deletion.</summary>
        public void DeleteMetaFile(string assetPath) => throw new NotSupportedException();

        /// <summary>Rejects unused invalid metadata cleanup.</summary>
        public Task DeleteInvalidMetaFiles() => throw new NotSupportedException();
    }

    /// <summary>Captures shader import paths.</summary>
    private sealed class TestAssetImporter : IAssetImporter
    {
        public event Action ImportedAssetsEvent = delegate { };
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting { get; } = new Observable<bool>(false);
        public List<IReadOnlyList<string>> Requests { get; } = new();

        /// <summary>Rejects unused full import.</summary>
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();

        /// <summary>Captures partial import paths.</summary>
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) { Requests.Add(paths.ToArray()); return Task.FromResult(new List<AssetInfo>()); }
    }

    /// <summary>Supplies fixed shader template.</summary>
    private sealed class TestProjectTemplateProvider : IProjectTemplateProvider
    {
        /// <summary>Rejects unused solution-template request.</summary>
        public Task<string> GetVSSolutionTemplate() => throw new NotSupportedException();

        /// <summary>Rejects unused project-template request.</summary>
        public Task<string> GetVSProjectTemplate() => throw new NotSupportedException();

        /// <summary>Rejects unused main-template request.</summary>
        public Task<string> GetMainFileTemplate() => throw new NotSupportedException();

        /// <summary>Returns shader template under test.</summary>
        public Task<string> GetNewShaderTemplate() => Task.FromResult("shader-template\npass");
    }


    /// <summary>
    /// Valid creation writes provider template unchanged, creates metadata identity, and reimports shader.
    /// </summary>
    [Fact]
    public async Task CreateShaderWritesTemplateMetadataAndImport()
    {
        using var project = new TemporaryProjectFixture();
        var resources = new TestResourceService(project.Resources);
        var metaFiles = new TestMetaFilesService();
        var importer = new TestAssetImporter();
        var utility = new ShaderCreationUtility(resources, new TestAssetCreator(), new TestAssetRegistry(), metaFiles, importer, new TestProjectTemplateProvider(), new TestLogger<ShaderCreationUtility>());

        var result = await utility.CreateShaderAsync(new ShaderCreationSettings(project.Directory.RootPath, "Lit Shader"));

        var path = project.Directory.GetPath("Lit Shader.rshader");
        Assert.True(result);
        Assert.Equal("shader-template\npass", await File.ReadAllTextAsync(path));
        Assert.Equal("shader-id", metaFiles.CreatedMeta!.AssetId);
        Assert.Equal(path, metaFiles.CreatedPath);
        Assert.Equal([path], Assert.Single(importer.Requests));
    }

    /// <summary>
    /// Invalid names, duplicate registry names, and missing target directories create no files or imports.
    /// </summary>
    [Theory]
    [InlineData("bad-name", false, true)]
    [InlineData("Existing", true, true)]
    [InlineData("Valid", false, false)]
    public async Task InvalidInputsCreateNoShader(string name, bool duplicate, bool targetExists)
    {
        using var project = new TemporaryProjectFixture();
        var target = targetExists ? project.Directory.RootPath : project.Directory.GetPath("Missing");
        var resources = new TestResourceService(project.Resources);
        var importer = new TestAssetImporter();
        var utility = new ShaderCreationUtility(resources, new TestAssetCreator(), new TestAssetRegistry { IsUnique = !duplicate }, new TestMetaFilesService(), importer, new TestProjectTemplateProvider(), new TestLogger<ShaderCreationUtility>());

        var result = await utility.CreateShaderAsync(new ShaderCreationSettings(target, name));

        Assert.False(result);
        Assert.Empty(resources.Writes);
        Assert.Empty(importer.Requests);
    }

    /// <summary>
    /// Rejected write prevents metadata creation and reimport.
    /// </summary>
    [Fact]
    public async Task WriteFailureStopsMetadataAndImport()
    {
        using var project = new TemporaryProjectFixture();
        var resources = new TestResourceService(project.Resources) { OnWrite = (_, _) => Task.FromResult(false) };
        var metaFiles = new TestMetaFilesService();
        var importer = new TestAssetImporter();
        var utility = new ShaderCreationUtility(resources, new TestAssetCreator(), new TestAssetRegistry(), metaFiles, importer, new TestProjectTemplateProvider(), new TestLogger<ShaderCreationUtility>());

        var result = await utility.CreateShaderAsync(new ShaderCreationSettings(project.Directory.RootPath, "Valid"));

        Assert.False(result);
        Assert.Null(metaFiles.CreatedMeta);
        Assert.Empty(importer.Requests);
    }

}
