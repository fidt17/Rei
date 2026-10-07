using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Condition;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Tests.Models.Services.Scenes;

[Trait("Category", "Unit")]
[Trait("Area", "SceneOpening")]
public sealed class SceneOpeningServiceTests
{
    private sealed class TestAssets(List<string> events) : IAssetsService
    {
        public Observable<bool> Saving { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> SaveInProcess => Saving;
        public Func<Task<Scene?>> LoadScene { get; set; } = () => Task.FromResult<Scene?>(CreateScene("next"));
        public async Task<T?> Load<T>(string assetId) where T : Asset => (T?)(Asset?)await LoadScene();
        public Task SaveProject() { events.Add("save"); return Task.CompletedTask; }
        public Task<T?> Load<T>(AssetInfo info) where T : Asset => throw new NotSupportedException();
        public Task<T?> LoadFrom<T>(string path) where T : Asset => throw new NotSupportedException();
        public void Unload(string id) => throw new NotSupportedException();
        public Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> extensions) => throw new NotSupportedException();
    }

    private sealed class TestScenes(List<string> events) : ISceneManagementService
    {
        public Observable<Scene?> Scene { get; } = new(SceneOpeningServiceTests.CreateScene("previous"));
        public ReiEditor.Utils.Common.IObservable<Scene?> CurrentScene => Scene;
        public Task LoadScene(Scene scene) { events.Add("load:" + scene.AssetId); Scene.Value = scene; return Task.CompletedTask; }
        public Task InitializeAsync() => throw new NotSupportedException();
        public Task<Scene?> CreateScene(string name, string path) => throw new NotSupportedException();
        public Task ReloadCurrentScene() => throw new NotSupportedException();
        public BuildScenesConfiguration GetBuildConfiguration() => throw new NotSupportedException();
        public void SetBuildSceneId(Scene scene, int id) => throw new NotSupportedException();
    }

    private sealed class TestBuild(List<string> events) : IBuildStarter
    {
        public bool Succeed { get; set; } = true;
        public ICondition CanStartBuild => throw new NotSupportedException();
        public Task<bool> BuildProject(BuildConfigurationEnum configurationEnum, bool forceSolutionRebuild = false, bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null, bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal(BuildConfigurationEnum.EditorDebug, configurationEnum);
            Assert.False(buildSolution);
            Assert.True(buildAssets);
            events.Add("build");
            return Task.FromResult(Succeed);
        }
    }

    private readonly List<string> _events = new();
    private readonly TestEngineRunner _engine = new();
    private readonly EditorProceduresService _procedures = new();
    private readonly TestLogger<SceneOpeningService> _logger = new();
    private readonly TestAssets _assets;
    private readonly TestScenes _scenes;
    private readonly TestBuild _build;
    private readonly SceneOpeningService _service;

    public SceneOpeningServiceTests()
    {
        _assets = new(_events);
        _scenes = new(_events);
        _build = new(_events);
        _engine.Active.Value = _engine.EditorActive.Value = true;
        _engine.OnStop = () => { _events.Add("stop"); _engine.Active.Value = _engine.EditorActive.Value = false; return Task.CompletedTask; };
        _engine.OnStart = mode => { Assert.Equal(EngineRunMode.EditorMode, mode); _events.Add("start:" + _scenes.CurrentScene.Value!.AssetId); _engine.Starting.Value = true; return true; };
        _service = new(_assets, _scenes, _engine, _build, _procedures, _logger);
    }

    [Fact]
    public async Task SavesPreviousAndSelectedScenesBeforeRestartingNativeEngine()
    {
        Assert.True(await _service.OpenAsync("next"));
        Assert.Equal(new[] { "stop", "save", "load:next", "save", "build", "start:next" }, _events);
        Assert.Equal("next", _scenes.CurrentScene.Value!.AssetId);
        Assert.False(_procedures.AnyActiveProcedures());
    }

    [Theory]
    [InlineData("play")]
    [InlineData("starting")]
    [InlineData("saving")]
    [InlineData("procedure")]
    public async Task BusyEditorDoesNotLoadOrStopEngine(string state)
    {
        _assets.LoadScene = () => throw new Exception("Must not load");
        if (state == "play") _engine.PlaymodeActive.Value = true;
        if (state == "starting") _engine.Starting.Value = true;
        if (state == "saving") _assets.Saving.Value = true;
        if (state == "procedure") _procedures.TrackProcedure(new Procedure("build"));
        Assert.False(await _service.OpenAsync("next"));
        Assert.Empty(_events);
        Assert.Single(_logger.Entries);
    }

    [Fact]
    public async Task CurrentSceneIsNoOp()
    {
        Assert.True(await _service.OpenAsync("previous"));
        Assert.Empty(_events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInvalidScenePreservesRunningEngine(bool throws)
    {
        _assets.LoadScene = () => throws ? throw new InvalidDataException("Invalid scene") : Task.FromResult<Scene?>(null);
        Assert.False(await _service.OpenAsync("next"));
        Assert.Empty(_events);
        Assert.Equal("previous", _scenes.CurrentScene.Value!.AssetId);
        Assert.Single(_logger.Entries);
    }

    [Fact]
    public async Task AssetBuildFailureRestoresPreviousSceneAndRestartsIt()
    {
        _build.Succeed = false;
        Assert.False(await _service.OpenAsync("next"));
        Assert.Equal(new[] { "stop", "save", "load:next", "save", "build", "load:previous", "save", "start:previous" }, _events);
        Assert.False(_procedures.AnyActiveProcedures());
    }

    [Fact]
    public async Task RepeatedClickCannotOverlapSceneLoading()
    {
        var pending = new TaskCompletionSource<Scene?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _assets.LoadScene = () => pending.Task;
        var first = _service.OpenAsync("next");
        Assert.False(await _service.OpenAsync("other"));
        pending.SetResult(CreateScene("next"));
        Assert.True(await first);
        Assert.Single(_events, e => e == "stop");
    }

    [Fact]
    public async Task RejectedEngineStartRestoresPreviousScene()
    {
        _engine.OnStart = _ =>
        {
            var id = _scenes.CurrentScene.Value!.AssetId;
            _events.Add("start:" + id);
            _engine.Starting.Value = id == "previous";
            return id == "previous";
        };
        Assert.False(await _service.OpenAsync("next"));
        Assert.Equal("previous", _scenes.CurrentScene.Value!.AssetId);
        Assert.Equal("start:previous", _events.Last());
        Assert.Single(_logger.Entries);
        Assert.False(_procedures.AnyActiveProcedures());
    }

    private static Scene CreateScene(string id)
    {
        var scene = new Scene(id);
        scene.SetAssetInfo(new AssetInfo(new AssetMeta(id), Path.Combine(Path.GetTempPath(), id + ".scene")));
        return scene;
    }
}
