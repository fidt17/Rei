using System.Reflection;
using Autofac;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.EditorApp.SettingsWindow;
using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Scenes.Templates;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;
using ReiEditor.ViewModels.Windows.Editor.Settings;
using ReiEditor.ViewModels.Windows.Editor.StatusBar;

namespace ReiEditor.Tests.Startup;

/// <summary>
/// Verifies production editor modules compose useful managed graphs without starting native or desktop infrastructure.
/// </summary>
[Trait("Category", "Composition")]
[Trait("Area", "Startup")]
public sealed class EditorModulesTests
{
    /// <summary>Rejects every unexpected call made through an external dependency supplied to a composition graph.</summary>
    public class TestRejectingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException($"Unexpected external dependency call: {targetMethod?.Name}.");
    }

    /// <summary>Stores viewport settings and records persistence without writing a profile.</summary>
    private sealed class TestPreferences : IEditorPreferencesService
    {
        public ViewportGridSettings Settings { get; } = new();
        public ViewportGridSettings? SavedSettings { get; private set; }
        public Task InitializeAsync() => Task.CompletedTask;
        public ViewportGridSettings GetGridSettings() => Settings;
        public void SetGridSettings(ViewportGridSettings settings) => SavedSettings = settings;
        public string? GetEnginePath() => throw new NotSupportedException();
        public void SetEnginePath(string path) => throw new NotSupportedException();
        public string? GetMsBuildPath() => throw new NotSupportedException();
        public void SetMsBuildPath(string path) => throw new NotSupportedException();
        public string? GetTextEditorPath() => throw new NotSupportedException();
        public void SetTextEditorPath(string path) => throw new NotSupportedException();
        public IEnumerable<Project> GetBookmarkedProjects() => throw new NotSupportedException();
        public void SetBookmarkedProjects(IEnumerable<Project> paths) => throw new NotSupportedException();
        public ConsolePreferences GetConsolePreferences() => throw new NotSupportedException();
        public void SetConsolePreferences(ConsolePreferences consolePreferences) => throw new NotSupportedException();
        public string GetWindowContainerActiveTab(string tag) => throw new NotSupportedException();
        public void SetWindowContainerActiveTab(string tag, string tabName) => throw new NotSupportedException();
    }

    /// <summary>
    /// AssetsModule resolves real singleton interfaces whose behavior identifies their production implementations.
    /// </summary>
    [Fact]
    public void TestAssetsModuleResolvesWorkingSingletonServices()
    {
        using var container = BuildContainer(new AssetsModule());

        var mapper = container.Resolve<IAssetTypeMapper>();
        var parser = container.Resolve<IShaderUniformParser>();

        Assert.Equal(AssetType.Font, mapper.GetAssetTypeForTemplateType("rei::render::Font"));
        Assert.Equal("albedo", Assert.Single(parser.ParseUniforms("uniform sampler2D albedo;")).Name);
        Assert.Same(mapper, container.Resolve<IAssetTypeMapper>());
        Assert.Same(parser, container.Resolve<IShaderUniformParser>());
    }

    /// <summary>
    /// SceneModule creates real transient templates and injects typed operation dependencies without resolving unsafe scene services.
    /// </summary>
    [Fact]
    public async Task TestSceneModuleResolvesTransientTemplateAndRunsMinimalSetup()
    {
        using var container = BuildContainer(new SceneModule());
        var entities = new TestEntityManagementService { OnCreate = (_, _) => Task.FromResult<GameEntity?>(null) };
        var registry = new TestBehaviourRegistry();
        var parameters = new Autofac.Core.Parameter[]
        {
            new TypedParameter(typeof(IEntityManagementService), entities),
            new TypedParameter(typeof(IBehaviourRegistry), registry)
        };

        var first = container.Resolve<DefaultSceneTemplate>(parameters);
        var second = container.Resolve<DefaultSceneTemplate>(parameters);
        await first.SetupScene();

        Assert.NotSame(first, second);
        Assert.Equal(new[] { "Main Camera", "Point Light" }, entities.CreateCalls.Select(call => call.Name));
    }

    /// <summary>
    /// BuildModule creates its real nonlazy tracker, which follows build state and loses ownership on root disposal.
    /// </summary>
    [Fact]
    public async Task TestBuildModuleOwnsNonLazyProcedureTrackerUntilContainerDisposal()
    {
        var procedures = new EditorProceduresService();
        var builder = new ContainerBuilder();
        RegisterBuildExternalDependencies(builder, procedures);
        builder.RegisterModule<BuildModule>();
        Observable<bool> buildState;

        await using (var container = builder.Build())
        {
            var buildService = Assert.IsType<BuildService>(container.Resolve<IBuildService>());
            buildState = GetBuildState(buildService);
            try
            {
                buildState.Value = true;
                Assert.Equal(ProcedureTags.BUILD_PROJECT, Assert.Single(procedures.ActiveProcedures).Name);
                buildState.Value = false;
                Assert.Empty(procedures.ActiveProcedures);
            }
            finally
            {
                buildState.Value = false;
            }
        }

        buildState.Value = true;
        Assert.Empty(procedures.ActiveProcedures);
    }

    /// <summary>
    /// EngineModule resolves its real API singleton and exposes safe managed state without loading a native DLL.
    /// </summary>
    [Fact]
    public void TestEngineModuleResolvesApiSingletonWithoutNativeActivation()
    {
        var builder = new ContainerBuilder();
        builder.RegisterGeneric(typeof(TestLogger<>)).As(typeof(ILogger<>));
        builder.RegisterModule<EngineModule>();
        using var container = builder.Build();
        var api = container.Resolve<IEngineApi>();

        Assert.IsType<EngineApi>(api);
        Assert.False(api.IsEngineRunning);
        Assert.Same(api, container.Resolve<IEngineApi>());
    }

    /// <summary>
    /// PlaymodeModule resolves its real viewport singleton and root disposal persists state and detaches subscriptions.
    /// </summary>
    [Fact]
    public void TestPlaymodeModuleOwnsViewportServiceAndDisposesSubscriptions()
    {
        var preferences = new TestPreferences();
        var api = CreateDependency<IEngineApi>();
        var runner = new TestEngineRunner();
        var container = BuildContainer(new PlaymodeModule());
        try
        {
            var service = container.Resolve<IViewportGridService>(
                new TypedParameter(typeof(IEditorPreferencesService), preferences),
                new TypedParameter(typeof(IEngineApi), api),
                new TypedParameter(typeof(IEngineRunner), runner));

            Assert.IsType<ViewportGridService>(service);
            Assert.Same(service, container.Resolve<IViewportGridService>());
            Assert.Equal(1, runner.EngineStartedSubscriberCount);
        }
        finally
        {
            container.Dispose();
        }

        Assert.Equal(0, runner.EngineStartedSubscriberCount);
        Assert.Same(preferences.Settings, preferences.SavedSettings);
    }

    /// <summary>
    /// EditorConsoleModule auto-creates its real event system, clears through real console service, and detaches on disposal.
    /// </summary>
    [Fact]
    public void TestEditorConsoleModuleOwnsNonLazyEventSystemUntilContainerDisposal()
    {
        var build = new TestBuildService();
        var runner = new TestEngineRunner();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(build).As<IBuildService>();
        builder.RegisterInstance(runner).As<IEngineRunner>();
        builder.RegisterModule<EditorConsoleModule>();
        var container = builder.Build();
        IEditorConsoleService console;
        var message = new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Info, DateTime.UtcNow, "one", "");

        try
        {
            console = container.Resolve<IEditorConsoleService>();
            Assert.IsType<EditorConsoleService>(console);
            Assert.Same(console, container.Resolve<IEditorConsoleService>());
            console.Log(message);
            build.InProgress.Value = true;
            Assert.Empty(console.Logs);
        }
        finally
        {
            container.Dispose();
        }

        console.Log(message);
        build.InProgress.Value = false;
        build.InProgress.Value = true;
        Assert.Same(message, Assert.Single(console.Logs));
    }

    /// <summary>
    /// MonitorModule creates a real drawer from a runtime entity parameter and disposal removes its entity subscription.
    /// </summary>
    [Fact]
    public void TestMonitorModuleResolvesDrawerAndDisposesEntitySubscription()
    {
        using var container = BuildContainer(new MonitorModule());
        var entity = new GameEntity(17, "Before");
        var entities = new TestEntityManagementService { OnRename = (target, name) => target.SetName(name) };
        var drawer = container.Resolve<EntityInfoComponentDrawerViewModel>(
            new TypedParameter(typeof(GameEntity), entity),
            new TypedParameter(typeof(IEntityManagementService), entities));

        drawer.EntityName = "During";
        Assert.Equal("During", entity.Name);
        Assert.Equal("17", drawer.SceneId);

        drawer.Dispose();
        entity.SetName("After");
        Assert.Equal("During", drawer.EntityName);
    }

    /// <summary>
    /// SettingsModule resolves its real service as a singleton without opening a desktop window.
    /// </summary>
    [Fact]
    public void TestSettingsModuleResolvesClosedSingletonService()
    {
        using var container = BuildContainer(new SettingsModule());
        var service = container.Resolve<ISettingsWindowService>(
            new TypedParameter(typeof(IFactory<EditorSettingsWindowViewModel>), CreateDependency<IFactory<EditorSettingsWindowViewModel>>()),
            new TypedParameter(typeof(ILogger<SettingsWindowService>), new TestLogger<SettingsWindowService>()),
            new TypedParameter(typeof(IMainWindowService), CreateDependency<IMainWindowService>()));

        Assert.IsType<SettingsWindowService>(service);
        Assert.False(service.IsOpened.Value);
        Assert.Same(service, container.Resolve<ISettingsWindowService>());
    }

    /// <summary>
    /// StatusBarModule creates a real transient view model whose procedure subscription ends on disposal.
    /// </summary>
    [Fact]
    public void TestStatusBarModuleTracksProceduresUntilViewModelDisposal()
    {
        using var container = BuildContainer(new StatusBarModule());
        var procedures = new EditorProceduresService();
        var viewModel = container.Resolve<StatusBarViewModel>(
            new TypedParameter(typeof(IEditorProceduresService), procedures));
        var procedure = new Procedure("Composing");

        procedures.TrackProcedure(procedure);
        Assert.True(viewModel.ShowStatusBar);
        Assert.Equal("Composing...", viewModel.ActiveProcedureText);
        procedure.Complete();
        Assert.False(viewModel.ShowStatusBar);

        viewModel.Dispose();
        procedures.TrackProcedure(new Procedure("After disposal"));
        Assert.False(viewModel.ShowStatusBar);
    }

    private static IContainer BuildContainer(Autofac.Module module)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(module);
        return builder.Build();
    }

    private static T CreateDependency<T>() where T : class => DispatchProxy.Create<T, TestRejectingProxy>();

    private static Observable<bool> GetBuildState(BuildService service)
    {
        var field = typeof(BuildService).GetField("_buildInProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<Observable<bool>>(field?.GetValue(service));
    }

    private static void RegisterBuildExternalDependencies(ContainerBuilder builder, IEditorProceduresService procedures)
    {
        builder.RegisterGeneric(typeof(TestLogger<>)).As(typeof(ILogger<>));
        builder.RegisterInstance(CreateDependency<IResourceService>()).As<IResourceService>();
        builder.RegisterInstance(CreateDependency<IBinarySerializer>()).As<IBinarySerializer>();
        builder.RegisterInstance(CreateDependency<IClientDllManager>()).As<IClientDllManager>();
        builder.RegisterInstance(CreateDependency<IEngineApi>()).As<IEngineApi>();
        builder.RegisterInstance(CreateDependency<IEngineLogger>()).As<IEngineLogger>();
        builder.RegisterInstance(CreateDependency<IAssetRegistry>()).As<IAssetRegistry>();
        builder.RegisterInstance(CreateDependency<IEngineSettingsProvider>()).As<IEngineSettingsProvider>();
        builder.RegisterInstance(CreateDependency<IAssetsService>()).As<IAssetsService>();
        builder.RegisterInstance(CreateDependency<IEditorPreferencesService>()).As<IEditorPreferencesService>();
        builder.RegisterInstance(CreateDependency<IActiveProjectService>()).As<IActiveProjectService>();
        builder.RegisterInstance(CreateDependency<IEditorConsoleService>()).As<IEditorConsoleService>();
        builder.RegisterInstance(new TestAssetImporter()).As<IAssetImporter>();
        builder.RegisterInstance(procedures).As<IEditorProceduresService>();
        builder.RegisterType<SourceFilesUtility>();
    }
}
