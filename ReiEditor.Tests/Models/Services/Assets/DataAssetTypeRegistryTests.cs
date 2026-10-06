using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets;

[Trait("Category", "FileSystem")]
[Trait("Area", "DataAssets")]
public sealed class DataAssetTypeRegistryTests : IDisposable
{
    private sealed class Creator : IAssetCreator
    {
        public string AllocateAssetId() => Guid.NewGuid().ToString();
        public Task<bool> Create(Asset asset, string projectPath) => throw new NotSupportedException();
        public Task<bool> Create(Asset asset, string id, string projectPath) => throw new NotSupportedException();
    }

    private sealed class Settings(string root) : IEngineSettingsProvider
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => root;
        public string GetEngineDebugIncludeDir() => root;
        public string GetEngineReleaseIncludeDir() => root;
        public string GetEngineSourceIncludes() => root;
        public string GetEngineResourcesDir() => Path.Combine(root, "resources");
        public string GetEngineBehavioursDir() => Path.Combine(GetEngineResourcesDir(), "rei_behaviours");
        public string GetEngineVersion() => "test";
    }

    private readonly TemporaryProjectFixture _project = new();
    private readonly Settings _settings;
    private readonly DataAssetTypeRegistry _registry;

    public DataAssetTypeRegistryTests()
    {
        _settings = new Settings(_project.Directory.GetPath("engine"));
        var metas = new MetaFilesService(_project.Resources, new JsonSerializer(), new TestLogger<MetaFilesService>());
        _registry = new DataAssetTypeRegistry(_project.Resources, metas, new Creator(), new TestLogger<DataAssetTypeRegistry>(), _settings);
    }

    [Fact]
    public async Task EngineDeclarationsKeepStableIdsInProjectInternalWithoutWritingEngine()
    {
        var source = Path.Combine(_settings.GetEngineResourcesDir(), "rei_data_assets", "render", "RendererSettings.h");
        var declaration = Declaration(source, "RendererSettings");
        await _registry.RefreshAsync([declaration]);
        var typeId = _registry.GetDataAssetType("RendererSettings")!.TypeId;
        var metaPath = _project.Resources.GetRootPath("Internal", "rei_data_assets", "render", "RendererSettings.h.meta");
        Assert.True(File.Exists(metaPath));
        Assert.False(File.Exists(source + ".meta"));
        Assert.False(Directory.Exists(_settings.GetEnginePath()));
        await _registry.RefreshAsync([declaration]);
        Assert.Equal(typeId, _registry.GetDataAssetType("RendererSettings")!.TypeId);
        Assert.Equal(typeId + 1, _registry.AllocateDataAssetTypeId());
    }

    [Fact]
    public async Task EngineAndProjectDeclarationsShareIdSpaceRegardlessOfEnumerationOrder()
    {
        var projectSource = _project.Resources.GetScriptsPath("GameSettings.h");
        var meta = new AssetMeta("existing");
        meta.AddData(DataAssetMeta.Key, new DataAssetMeta(7));
        await _project.Resources.Write(new JsonSerializer().Serialize(meta), projectSource + ".meta");
        await _registry.RefreshAsync([
            Declaration(Path.Combine(_settings.GetEngineResourcesDir(), "RendererSettings.h"), "RendererSettings"),
            Declaration(projectSource, "GameSettings")]);
        Assert.Equal(7, _registry.GetDataAssetType("GameSettings")!.TypeId);
        Assert.Equal(8, _registry.GetDataAssetType("RendererSettings")!.TypeId);
    }

    [Theory]
    [InlineData("external")]
    [InlineData("engine/resources-sibling")]
    [InlineData("engine/resources/../../external")]
    public async Task UnrelatedSourcesAreRejectedWithoutReplacingValidRegistry(string relativePath)
    {
        var valid = Declaration(_project.Resources.GetScriptsPath("Valid.h"), "Valid");
        await _registry.RefreshAsync([valid]);
        var rejected = Declaration(_project.Directory.GetPath(relativePath, "Invalid.h"), "Invalid");
        await Assert.ThrowsAsync<Exception>(() => _registry.RefreshAsync([rejected]));
        Assert.Single(_registry.GetDataAssetTypes());
        Assert.NotNull(_registry.GetDataAssetType("Valid"));
        Assert.False(File.Exists(rejected.Source.FullPath + ".meta"));
    }

    [Fact]
    public void CameraProfileDefaultIsAnEmptyAssetId()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var assetRefPath = Path.Combine(repo, "Rei", "src", "Modules", "Assets", "Core", "AssetRef.h");
        var cameraPath = Path.Combine(repo, "Rei", "resources", "rei_behaviours", "render", "camera", "Camera.h");
        var parser = new ReiEditor.Models.Services.Assets.Scripting.SourceFilesUtility(_project.Resources, _settings,
            new TestLogger<ReiEditor.Models.Services.Assets.Scripting.SourceFilesUtility>());
        var assetRef = new SerializableObjectInfo("rei::assets", "AssetRef", true,
            new ObjectFile<string>("", assetRefPath), parser.GetSerializedProperties(File.ReadAllText(assetRefPath)), assetRefPath);
        var objects = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        objects.Replace([assetRef], []);
        var properties = new SerializedPropertiesService(objects, new TestLogger<SerializedPropertiesService>());
        var profile = properties.Create("_rendererSettings", parser.GetSerializedProperties(File.ReadAllText(cameraPath))["_rendererSettings"], null);
        var fields = Assert.IsType<Dictionary<string, ReiEditor.Models.Services.Components.SerializedProperty>>(profile.Value);
        Assert.Equal("", fields["Id"].Value);
    }

    private static SerializableObjectInfo Declaration(string path, string name) => new("game", name, false, new ObjectFile<string>("", path), new(), path);

    public void Dispose() => _project.Dispose();
}
