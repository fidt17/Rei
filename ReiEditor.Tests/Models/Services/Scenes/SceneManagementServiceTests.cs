using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>Verifies scene loading, persisted transforms, build configuration and reload behavior.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "SceneManagement")]
public sealed class SceneManagementServiceTests
{
    private sealed class SceneLoader : IEngineSceneLoader
    {
        public Func<Scene?, Task<bool>> Load { get; set; } = _ => Task.FromResult(true);
        public Task<bool> LoadSceneAsync(Scene? scene) => Load(scene);
    }

    private sealed class NativeApi : TestEngineApi, IEngineNativeAccess
    {
        public string? SceneId = "before";
        public bool TryInvoke(Action<IEngineApi> action) { action(this); return true; }
        public override bool LoadEditorScene(string json)
        {
            var request = JObject.Parse(json);
            SceneId = request["Scene"]!.Type == JTokenType.Null ? null : request["SceneId"]!.Value<string>();
            return SceneId != null;
        }
    }

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
    private readonly SceneLoader _loader = new();
    private readonly Project _project = new();
    private readonly SelectionService _selection = new(new TestEntityApi());

    [Fact]
    public async Task SceneSelectionUpdatesExistingLastSceneIdWithoutSavingProjectOrGameConfiguration()
    {
        using var files = new TemporaryDirectory();
        var configPath = files.GetPath("Build Scenes Configuration.asset");
        const string GAME_CONFIG = "{\"Scenes\":{\"0\":\"game-start\"}}";
        const string PROJECT_SETTINGS = "{\"LastSceneId\":\"before\"}";
        await File.WriteAllTextAsync(configPath, GAME_CONFIG);
        var config = new BuildScenesConfiguration();
        config.Scenes[0] = "game-start";
        config.SetAssetInfo(new AssetInfo(new AssetMeta("0"), configPath));
        _assets.OnLoadFrom = _ => Task.FromResult<Asset?>(config);
        _project.SetProjectFilePath(files.GetPath("Test.rei"));
        _project.SetLastScene("before");
        await File.WriteAllTextAsync(_project.ProjectFilePath, PROJECT_SETTINGS);
        using var service = CreateService();
        await service.InitializeAsync();
        foreach (var id in new[] { "editor-a", "editor-b" })
        {
            await service.LoadScene(SceneWithId(id));
            Assert.Equal(id, _project.LastSceneId);
            Assert.Equal(PROJECT_SETTINGS, await File.ReadAllTextAsync(_project.ProjectFilePath));
            Assert.Equal(GAME_CONFIG, await File.ReadAllTextAsync(configPath));
            Assert.Equal("game-start", config.Scenes[0]);
        }
        _runner.Active.Value = true;
        _loader.Load = _ => Task.FromResult(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadScene(SceneWithId("invalid")));
        Assert.Equal("editor-b", _project.LastSceneId);
    }

    [Fact]
    public async Task UnreadableSceneClearsEditorAndNativeBeforeReportingFailure()
    {
        using var service = CreateService();
        await service.LoadScene(SceneWithId("before"));
        _runner.Active.Value = true;
        _assets.OnLoad = _ => Task.FromResult<Asset?>(null);
        var unloaded = false;
        _loader.Load = scene =>
        {
            Assert.Null(scene);
            Assert.Null(service.CurrentScene.Value);
            unloaded = true;
            return Task.FromResult(false);
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenScene("unreadable"));
        Assert.True(unloaded);
        Assert.Null(service.CurrentScene.Value);
    }

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

    /// <summary>Reload replaces the scene from disk; unreadable data leaves the transition empty.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReloadReplacesSceneOrClearsFailedTransition(bool found)
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
        if (found) await service.ReloadCurrentScene();
        else await Assert.ThrowsAsync<InvalidOperationException>(service.ReloadCurrentScene);
        Assert.Same(found ? replacement : null, service.CurrentScene.Value);
        Assert.Equal(new[] { "level" }, _assets.Loaded);
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

    private SceneManagementService CreateService(IEngineSceneLoader? loader = null)
    {
        var activeProject = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        activeProject.OpenProject(_project);
        return new(_logger, _assets, activeProject, _assets, _selection, _components, _registry, _runner, loader ?? _loader);
    }

    private static Scene SceneWithId(string id)
    {
        var scene = new Scene(id);
        scene.SetAssetInfo(new AssetInfo(new AssetMeta(id), Path.Combine(Path.GetTempPath(), id + ".scene")));
        return scene;
    }

    [Fact]
    public async Task ActiveTransitionPublishesSceneAfterNativeCompletion()
    {
        using var service = CreateService();
        await service.LoadScene(SceneWithId("before"));
        _runner.Active.Value = true;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var next = SceneWithId("after");
        _loader.Load = scene =>
        {
            Assert.Same(next, scene);
            Assert.Null(service.CurrentScene.Value);
            Assert.Empty(_selection.SelectedItems);
            Assert.Equal("before", _project.LastSceneId);
            return completed.Task;
        };
        var transition = service.LoadScene(next);
        Assert.False(transition.IsCompleted);
        completed.SetResult(true);
        await transition.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(next, service.CurrentScene.Value);
        Assert.Equal("after", _project.LastSceneId);
    }

    [Fact]
    public async Task FailedNativeTransitionStaysEmptyAndAllowsNextLoad()
    {
        using var service = CreateService();
        await service.LoadScene(SceneWithId("before"));
        _runner.Active.Value = true;
        _loader.Load = _ => Task.FromResult(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadScene(SceneWithId("invalid")));
        Assert.Null(service.CurrentScene.Value);
        Assert.Equal("before", _project.LastSceneId);
        _loader.Load = _ => Task.FromResult(true);
        await service.LoadScene(SceneWithId("recovered"));
        Assert.Equal("recovered", service.CurrentScene.Value!.AssetId);
    }

    [Fact]
    public async Task ConcurrentTransitionsRunInOrder()
    {
        using var service = CreateService();
        _runner.Active.Value = true;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        _loader.Load = scene =>
        {
            calls.Add(scene!.AssetId);
            return calls.Count == 1 ? completion.Task : Task.FromResult(true);
        };
        var first = service.LoadScene(SceneWithId("first"));
        var second = service.LoadScene(SceneWithId("second"));
        Assert.Equal(new[] { "first" }, calls);
        completion.SetResult(true);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "first", "second" }, calls);
        Assert.Equal("second", service.CurrentScene.Value!.AssetId);
    }

    [Fact]
    public async Task OpenSerializesFileReadsBeforeNativeTransitions()
    {
        using var service = CreateService();
        var slow = new TaskCompletionSource<Asset?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = SceneWithId("second");
        _assets.OnLoad = id => id == "first" ? slow.Task : Task.FromResult<Asset?>(second);
        var firstTask = service.OpenScene("first");
        var secondTask = service.OpenScene("second");
        Assert.Equal(new[] { "first" }, _assets.Loaded);
        Assert.False(secondTask.IsCompleted);
        slow.SetResult(SceneWithId("first"));
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "first", "second" }, _assets.Loaded);
        Assert.Same(second, service.CurrentScene.Value);
    }

    [Fact]
    public async Task SerializationFailureClearsEditorAndNativeAndAllowsRecovery()
    {
        var native = new NativeApi();
        var logger = new TestLogger<EngineSceneLoader>();
        using var service = CreateService(new EngineSceneLoader(native, logger));
        await service.LoadScene(SceneWithId("before"));
        _runner.Active.Value = true;
        _components.OnRefresh = _ => { };
        var invalid = SceneWithId("invalid");
        var entity = new GameEntity(1, "invalid");
        var behaviour = new BehaviourComponent(42);
        var property = SerializedPropertyBuilder.Integer("value", 1);
        behaviour.AddProperty(property);
        property.SetName(null!); // Malformed metadata must fail during request serialization.
        entity.AddBehaviour(behaviour);
        invalid.AddEntity(entity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadScene(invalid));
        Assert.Null(service.CurrentScene.Value);
        Assert.Null(native.SceneId);
        Assert.Equal("before", _project.LastSceneId);
        Assert.NotNull(Assert.Single(logger.Entries).Exception);
        await service.LoadScene(SceneWithId("recovered"));
        Assert.Equal("recovered", native.SceneId);
        Assert.Equal("recovered", service.CurrentScene.Value!.AssetId);
    }

    [Theory]
    [InlineData(EngineRunMode.EditorMode)]
    [InlineData(EngineRunMode.PlayMode)]
    public async Task RuntimeSceneAdoptionPreservesEditorStartupScene(EngineRunMode mode)
    {
        using var service = CreateService();
        var editorScene = SceneWithId("editor");
        await service.LoadScene(editorScene);
        _runner.ActiveMode = mode;
        var runtime = service.SynchronizeRuntimeScene("runtime", "Game scene");
        Assert.Equal("editor", _project.LastSceneId);
        if (mode == EngineRunMode.EditorMode)
        {
            Assert.Null(runtime);
            Assert.Same(editorScene, service.CurrentScene.Value);
            return;
        }
        Assert.Same(runtime, service.CurrentScene.Value);
        Assert.Equal("runtime", runtime!.AssetId);
        Assert.Equal("Game scene", runtime.Name);
        Assert.Null(service.SynchronizeRuntimeScene("", ""));
        Assert.Null(service.CurrentScene.Value);
        _assets.OnLoad = id => { Assert.Equal("editor", id); return Task.FromResult<Asset?>(editorScene); };
        await service.ReloadCurrentScene();
        Assert.Same(editorScene, service.CurrentScene.Value);
    }

    [Fact]
    public async Task RuntimeSnapshotCannotOverridePendingEditorTransition()
    {
        using var service = CreateService();
        await service.LoadScene(SceneWithId("editor"));
        _runner.ActiveMode = EngineRunMode.PlayMode;
        var pending = new TaskCompletionSource<Asset?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _assets.OnLoad = _ => pending.Task;
        var open = service.OpenScene("next");
        Assert.Null(service.SynchronizeRuntimeScene("old-runtime", "Old runtime"));
        pending.SetResult(SceneWithId("next"));
        await open;
        Assert.Equal("next", service.CurrentScene.Value!.AssetId);
    }
}
