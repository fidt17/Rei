using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>Verifies scene loading, persisted transforms, build configuration and reload behavior.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "SceneManagement")]
public sealed class SceneManagementServiceTests
{
    private sealed class TestSelectable : ISelectable
    {
        public void Select() { }
        public void Deselect() { }
    }

    private sealed class TestAssets : IAssetsService, IAssetCreator
    {
        public Func<string, Task<Asset?>>? OnLoad { get; set; }
        public Func<string, Task<Asset?>>? OnLoadFrom { get; set; }
        public Func<Asset, string?, string, Task<bool>>? OnCreate { get; set; }
        public List<string> Unloaded { get; } = new();
        public List<string> Loaded { get; } = new();
        public ReiEditor.Utils.Common.IObservable<bool> SaveInProcess => throw new NotSupportedException();
        public async Task<T?> Load<T>(string assetId) where T : Asset
        {
            Loaded.Add(assetId);
            return (T?)await (OnLoad ?? throw new NotSupportedException())(assetId);
        }
        public async Task<T?> LoadFrom<T>(string projectPath) where T : Asset => (T?)await (OnLoadFrom ?? throw new NotSupportedException())(projectPath);
        public Task<T?> Load<T>(AssetInfo assetInfo) where T : Asset => throw new NotSupportedException();
        public void Unload(string assetId) => Unloaded.Add(assetId);
        public Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> ignoredExtensions) => throw new NotSupportedException();
        public Task SaveProject() => throw new NotSupportedException();
        public string AllocateAssetId() => throw new NotSupportedException();
        public Task<bool> Create(Asset asset, string projectPath) => (OnCreate ?? throw new NotSupportedException())(asset, null, projectPath);
        public Task<bool> Create(Asset asset, string id, string projectPath) => (OnCreate ?? throw new NotSupportedException())(asset, id, projectPath);
    }

    private readonly TestAssets _assets = new();
    private readonly TestLogger<SceneManagementService> _logger = new();
    private readonly TestBehaviourComponentsService _components = new();
    private readonly TestBehaviourRegistry _registry = new();
    private readonly TestEngineRunner _runner = new();
    private readonly Project _project = new();
    private readonly SelectionService _selection = new(new TestEntityApi());

    /// <summary>Initialization loads existing configuration or creates one at the reserved path and ID.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeLoadsOrCreatesBuildConfiguration(bool exists)
    {
        var config = new BuildScenesConfiguration();
        config.Scenes[2] = "saved";
        var creations = new List<Asset>();
        _assets.OnLoadFrom = path =>
        {
            Assert.Equal("Settings/Build/Build Scenes Configuration.asset", path);
            return Task.FromResult<Asset?>(exists ? config : null);
        };
        _assets.OnCreate = (asset, id, path) =>
        {
            Assert.Equal(SpecialAssetIds.BUILD_SCENES_CONFIGURATION, id);
            Assert.Equal("Settings/Build/Build Scenes Configuration.asset", path);
            creations.Add(asset);
            return Task.FromResult(true);
        };
        using var service = CreateService();
        Assert.Throws<NullReferenceException>(() => service.GetBuildConfiguration());
        await service.InitializeAsync();
        if (exists)
        {
            Assert.Same(config, service.GetBuildConfiguration());
            Assert.Equal("saved", service.GetBuildConfiguration().Scenes[2]);
            Assert.Empty(creations);
        }
        else Assert.Same(Assert.Single(creations), service.GetBuildConfiguration());
        var scene = SceneWithId("build-scene");
        service.SetBuildSceneId(scene, 2);
        Assert.Equal("build-scene", service.GetBuildConfiguration().Scenes[2]);
    }

    /// <summary>Load refreshes components before restoring hierarchy and publishes the selected scene before resetting selection.</summary>
    [Fact]
    public async Task LoadRestoresTransformsAndPublishesCompletedScene()
    {
        var scene = SceneWithId("loaded");
        var parent = new GameEntity(1, "parent");
        var child = new GameEntity(2, "child");
        scene.AddEntity(child);
        scene.AddEntity(parent);
        _registry.Ids[EngineBehavioursConstants.TRANSFORM] = 10;
        var refreshed = new List<GameEntity>();
        _components.OnRefresh = entity =>
        {
            refreshed.Add(entity);
            var transform = new BehaviourComponent(10);
            transform.AddProperty(SerializedPropertyBuilder.Integer(EngineBehavioursConstants.TRANSFORM_PARENT, entity == child ? 1 : 0));
            transform.AddProperty(SerializedPropertyBuilder.Integer(EngineBehavioursConstants.TRANSFORM_ORDER, 8));
            entity.AddBehaviour(transform);
        };
        using var service = CreateService();
        var events = new List<string>();
        var selected = new TestSelectable();
        _selection.RegisterSelectable(selected);
        _selection.Select(selected);
        service.CurrentScene.Subscribe(value =>
        {
            Assert.Same(scene, value);
            Assert.Equal(new[] { child, parent }, refreshed);
            Assert.Equal("loaded", _project.LastSceneId);
            Assert.Same(scene.Hierarchy.GetNode(parent), scene.Hierarchy.GetNode(child)!.Parent);
            Assert.Equal(0, child.Transform.Order);
            Assert.Equal(0, parent.Transform.Order);
            Assert.True(_selection.IsSelected(selected));
            events.Add("scene");
        }, invoke: false);
        _selection.SelectionChanged.Subscribe(_ => events.Add("selection"), invoke: false);
        await service.LoadScene(scene);
        Assert.Same(scene, service.CurrentScene.Value);
        Assert.Equal(new[] { "scene", "selection" }, events);
        Assert.False(_selection.IsSelected(selected));
        Assert.Empty(_selection.SelectedItems);
    }

    /// <summary>Scene creation passes its name/path to asset storage and reports rejection or exceptions as null.</summary>
    [Theory]
    [InlineData("success")]
    [InlineData("rejected")]
    [InlineData("exception")]
    public async Task CreateSceneReportsStorageResult(string result)
    {
        _assets.OnCreate = (asset, id, path) =>
        {
            Assert.Equal("Levels/Level.scene", path);
            Assert.Equal("Level", Assert.IsType<Scene>(asset).Name);
            Assert.Null(id);
            return result == "exception" ? Task.FromException<bool>(new IOException("write")) : Task.FromResult(result == "success");
        };
        using var service = CreateService();
        var created = await service.CreateScene("Level", "Levels");
        if (result == "success") Assert.Equal("Level", Assert.IsType<Scene>(created).Name);
        else
        {
            Assert.Null(created);
            Assert.NotNull(Assert.Single(_logger.Entries).Exception);
        }
        Assert.Null(service.CurrentScene.Value);
    }

    /// <summary>Reload unloads the previous asset first, replaces it when found and retains it when missing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReloadPreservesOrReplacesCurrentScene(bool found)
    {
        var original = SceneWithId("level");
        var replacement = SceneWithId("level");
        using var service = CreateService();
        await service.LoadScene(original);
        _assets.OnLoad = id =>
        {
            Assert.Equal("level", id);
            Assert.Equal(new[] { "level" }, _assets.Unloaded);
            return Task.FromResult<Asset?>(found ? replacement : null);
        };
        await service.ReloadCurrentScene();
        Assert.Same(found ? replacement : original, service.CurrentScene.Value);
        Assert.Equal(new[] { "level" }, _assets.Loaded);
        Assert.Equal(found ? 0 : 1, _logger.Entries.Count(x => x.Level == ReiEditor.Models.Services.Logging.LogLevelEnum.Error));
    }

    /// <summary>Reload with no current scene logs the missing state without accessing assets.</summary>
    [Fact]
    public async Task MissingCurrentSceneAvoidsAssetAccess()
    {
        using var service = CreateService();
        await service.ReloadCurrentScene();
        Assert.Empty(_assets.Loaded);
        Assert.Empty(_assets.Unloaded);
        Assert.Contains("current scene is missing", Assert.Single(_logger.Entries).Message);
    }

    private SceneManagementService CreateService()
    {
        var activeProject = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        activeProject.OpenProject(_project);
        return new(_logger, _assets, activeProject, _assets, _selection, _components, _registry, _runner);
    }

    private static Scene SceneWithId(string id)
    {
        var scene = new Scene(id);
        scene.SetAssetInfo(new AssetInfo(new AssetMeta(id), Path.Combine(Path.GetTempPath(), id + ".scene")));
        return scene;
    }
}
