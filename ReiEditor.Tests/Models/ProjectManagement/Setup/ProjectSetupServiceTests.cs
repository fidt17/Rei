using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.ProjectManagement.Update;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Scenes.Templates;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Tests.Models.ProjectManagement.Setup;

/// <summary>Verifies project setup, scene fallback and asynchronous procedure lifetime without a native engine.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Projects")]
public sealed class ProjectSetupServiceTests
{
    private sealed class TestProjectUpdateService(Func<Project, Task> update) : IProjectUpdateService
    {
        public Task UpdateProject(Project project) => update(project);
    }

    private sealed class TestScenes(List<string> calls) : ISceneManagementService
    {
        public Observable<Scene?> Scene { get; } = new(null);
        public ReiEditor.Utils.Common.IObservable<Scene?> CurrentScene => Scene;
        public BuildScenesConfiguration Configuration { get; } = new();
        public Scene? CreatedScene { get; set; }
        public Func<Task> OnInitialize { get; set; } = () => Task.CompletedTask;
        public List<(string Name, string Path)> CreateCalls { get; } = new();
        public List<Scene> Loaded { get; } = new();

        public Task InitializeAsync() { calls.Add("initialize"); return OnInitialize(); }
        public Task<Scene?> CreateScene(string name, string projectPath)
        {
            calls.Add("create");
            CreateCalls.Add((name, projectPath));
            return Task.FromResult(CreatedScene);
        }
        public Task LoadScene(Scene scene)
        {
            calls.Add("load:" + scene.AssetId);
            Loaded.Add(scene);
            Scene.Value = scene;
            return Task.CompletedTask;
        }
        public Task ReloadCurrentScene() => throw new NotSupportedException();
        public BuildScenesConfiguration GetBuildConfiguration() => Configuration;
        public void SetBuildSceneId(Scene scene, int id) => Configuration.Scenes[id] = scene.AssetId;
    }

    private sealed class TestAssets(List<string> calls) : IAssetsService
    {
        public ReiEditor.Utils.Common.IObservable<bool> SaveInProcess => throw new NotSupportedException();
        public Dictionary<string, Scene> Available { get; } = new();
        public List<string> LoadCalls { get; } = new();
        public Func<Task> OnSave { get; set; } = () => Task.CompletedTask;
        public Task<T?> Load<T>(string assetId) where T : Asset
        {
            LoadCalls.Add(assetId);
            return Task.FromResult(Available.TryGetValue(assetId, out var scene) ? (T?)(Asset)scene : null);
        }
        public Task SaveProject() { calls.Add("save"); return OnSave(); }
        public Task<T?> LoadFrom<T>(string projectPath) where T : Asset => throw new NotSupportedException();
        public Task<T?> Load<T>(AssetInfo info) where T : Asset => throw new NotSupportedException();
        public void Unload(string assetId) => throw new NotSupportedException();
        public Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> ignoredExtensions) => throw new NotSupportedException();
    }

    private sealed class TestBuildStarter(List<string> calls) : IBuildStarter
    {
        public ICondition CanStartBuild => throw new NotSupportedException();
        public Func<Task<bool>> OnBuild { get; set; } = () => Task.FromResult(true);
        public Task<bool> BuildProject(BuildConfigurationEnum configurationEnum, bool forceSolutionRebuild = false, bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null, bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal(BuildConfigurationEnum.EditorDebug, configurationEnum);
            Assert.False(forceSolutionRebuild || forceCleanSolutionBuild || forceAssetRebuild);
            Assert.True(buildSolution && buildAssets);
            Assert.Null(buildContext);
            calls.Add("build");
            return OnBuild();
        }
    }

    private sealed class TestContext : IDisposable
    {
        public TemporaryProjectFixture Fixture { get; } = new();
        public List<string> Calls { get; } = new();
        public EditorProceduresService Procedures { get; } = new();
        public TestScenes Scenes { get; }
        public TestAssets Assets { get; }
        public TestBuildStarter Build { get; }
        public TestEntityManagementService Entities { get; }
        public TestLogger<ProjectSetupService> Logger { get; } = new();
        public Func<Task> OnUpdate { get; set; } = () => Task.CompletedTask;
        public ProjectSetupService Service { get; }

        public TestContext()
        {
            Scenes = new(Calls);
            Assets = new(Calls);
            Build = new(Calls);
            var active = new ActiveProjectService(new TestLogger<ActiveProjectService>());
            active.OpenProject(Fixture.Project);
            // The template's own entity/component output is covered separately; here its invocation is observable.
            Entities = new() { OnCreate = (name, _) => { Calls.Add("template:" + name); return Task.FromResult<ReiEditor.Models.Services.Entities.GameEntity?>(null); } };
            Service = new(Logger, Scenes, active, Assets, Procedures,
                new TestProjectUpdateService(project => { Assert.Same(Fixture.Project, project); Calls.Add("update"); return OnUpdate(); }),
                new DefaultSceneTemplate(Entities, new TestBehaviourRegistry()), Build, Fixture.Resources);
        }

        public Scene CreateScene(string id)
        {
            var scene = new Scene(id);
            scene.SetAssetInfo(new AssetInfo(new AssetMeta(id), Fixture.Resources.GetProjectPath("Scenes", id + ".scene")));
            return scene;
        }

        public void Dispose() => Fixture.Dispose();
    }

    /// <summary>A new project creates and templates a unique default scene, persists setup state, then builds.</summary>
    [Fact]
    public async Task NewProjectCreatesDefaultSceneAndSavesBeforeBuild()
    {
        using var context = new TestContext();
        var scene = context.CreateScene("default");
        context.Scenes.CreatedScene = scene;
        var existingPath = context.Fixture.Resources.GetProjectPath("Scenes", "New Scene.scene");
        Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
        await File.WriteAllTextAsync(existingPath, "existing");
        context.Assets.OnSave = () =>
        {
            Assert.True(context.Fixture.Project.HasBeenSetup);
            Assert.Equal(scene.AssetId, context.Fixture.Project.LastSceneId);
            Assert.Equal(scene.AssetId, context.Scenes.Configuration.Scenes[0]);
            Assert.True(context.Procedures.AnyActiveProcedures());
            return Task.CompletedTask;
        };

        await context.Service.PrepareProject();

        Assert.Equal(new[] { "update", "initialize", "create", "load:default", "template:Main Camera", "template:Point Light", "save", "build" }, context.Calls);
        var creation = Assert.Single(context.Scenes.CreateCalls);
        Assert.Equal("Scenes", creation.Path);
        Assert.StartsWith("New Scene", creation.Name);
        Assert.NotEqual("New Scene", creation.Name);
        Assert.False(File.Exists(context.Fixture.Resources.GetProjectPath("Scenes", creation.Name + ".scene")));
        Assert.Equal("existing", await File.ReadAllTextAsync(existingPath));
        Assert.Same(scene, Assert.Single(context.Scenes.Loaded));
        Assert.False(context.Procedures.AnyActiveProcedures());
    }

    /// <summary>An existing project opens its last scene or a configured build scene without recreating or saving it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingProjectLoadsLastOrBuildScene(bool lastSceneExists)
    {
        using var context = new TestContext();
        context.Fixture.Project.SetHasBeenSetup(true);
        context.Fixture.Project.SetLastScene("last");
        context.Scenes.Configuration.Scenes[0] = "fallback";
        var selected = context.CreateScene(lastSceneExists ? "last" : "fallback");
        context.Assets.Available[selected.AssetId] = selected;

        await context.Service.PrepareProject();

        Assert.Equal(lastSceneExists ? new[] { "last" } : new[] { "last", "fallback" }, context.Assets.LoadCalls);
        Assert.Same(selected, Assert.Single(context.Scenes.Loaded));
        Assert.Equal(new[] { "update", "initialize", "load:" + selected.AssetId, "build" }, context.Calls);
        Assert.Empty(context.Scenes.CreateCalls);
        Assert.False(context.Procedures.AnyActiveProcedures());
    }

    /// <summary>Missing last and build scenes fall back to a generated default, including an empty build configuration.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingScenesCreateDefault(bool emptyBuildConfiguration)
    {
        using var context = new TestContext();
        context.Fixture.Project.SetHasBeenSetup(true);
        context.Fixture.Project.SetLastScene("missing");
        if (!emptyBuildConfiguration) context.Scenes.Configuration.Scenes[0] = "also-missing";
        var scene = context.CreateScene("replacement");
        context.Scenes.CreatedScene = scene;

        await context.Service.PrepareProject();

        Assert.Same(scene, Assert.Single(context.Scenes.Loaded));
        Assert.Single(context.Scenes.CreateCalls);
        Assert.Equal("replacement", context.Scenes.Configuration.Scenes[0]);
        Assert.Equal(2, context.Entities.CreateCalls.Count);
        Assert.Equal("build", context.Calls.Last());
        Assert.False(context.Procedures.AnyActiveProcedures());
    }

    /// <summary>The loading procedure stays active until an asynchronous build finishes, even when build returns false.</summary>
    [Fact]
    public async Task ProcedureWaitsForBuildCompletion()
    {
        using var context = new TestContext();
        context.Fixture.Project.SetHasBeenSetup(true);
        context.Fixture.Project.SetLastScene("last");
        context.Assets.Available["last"] = context.CreateScene("last");
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Build.OnBuild = () => gate.Task;
        var task = context.Service.PrepareProject();
        try
        {
            Assert.False(task.IsCompleted);
            Assert.Single(context.Procedures.ActiveProcedures);
            gate.SetResult(false);
            await task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(context.Procedures.AnyActiveProcedures());
        }
        finally
        {
            gate.TrySetResult(false);
            await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Update, initialization, saving and build errors propagate while always ending the loading procedure.</summary>
    [Theory]
    [InlineData("update")]
    [InlineData("initialize")]
    [InlineData("save")]
    [InlineData("build")]
    public async Task FailedStageEndsLoadingProcedure(string stage)
    {
        using var context = new TestContext();
        var failure = new IOException("controlled setup failure");
        context.Scenes.CreatedScene = context.CreateScene("default");
        switch (stage)
        {
            case "update": context.OnUpdate = () => Task.FromException(failure); break;
            case "initialize": context.Scenes.OnInitialize = () => Task.FromException(failure); break;
            case "save": context.Assets.OnSave = () => Task.FromException(failure); break;
            case "build": context.Build.OnBuild = () => Task.FromException<bool>(failure); break;
        }

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => context.Service.PrepareProject()));
        Assert.Equal(stage, context.Calls.Last());
        Assert.False(context.Procedures.AnyActiveProcedures());
    }
}
