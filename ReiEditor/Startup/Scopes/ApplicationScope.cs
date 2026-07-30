using System.Threading.Tasks;
using Autofac;
using Avalonia.Platform.Storage;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.EditorApp.Shutdown;
using ReiEditor.Models.EditorApp.Storage;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Resources.Editor;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Mcp.Editor;
using ReiEditor.Models.Services.Mcp.Hosting;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Utils.Extensions;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows;

namespace ReiEditor.Startup.Scopes;

public class ApplicationScope : BaseLifetimeScope
{
    public ApplicationScope() : base(nameof(ApplicationScope)) { }
	
    protected override async Task OnScopeStart()
    {
        await Scope.Resolve<IEditorPreferencesService>().InitializeAsync();
        await Scope.Resolve<IEditorSettingsService>().InitializeAsync();
        await Scope.Resolve<IEngineSettingsProvider>().InitializeAsync();
        await Scope.Resolve<IMcpHostLifecycleService>().StartAsync();
		
        Scope.Resolve<ApplicationEntryPoint>().Start();
    }

    protected override void ConfigureContainer(ContainerBuilder b)
    {
        b.RegisterInstance(this);
        b.RegisterSingleton<ApplicationShutdownService>().As<IApplicationShutdownService>();
		
        b.RegisterGeneric(typeof(SystemConsoleLogger<>)).As(typeof(ILogger<>)).AsSelf();
		
        b.RegisterGeneric(typeof(Factory<>)).As(typeof(IFactory<>));
        b.RegisterModule<SerializationModule>();

        b.RegisterSingleton<McpEditorSessionAccessor>().As<IMcpEditorSessionAccessor>();
        b.RegisterSingleton<AvaloniaEditorThreadDispatcher>().As<IEditorThreadDispatcher>();
        b.RegisterSingleton<McpEditorGateway>().As<IReiEditorGateway>();
        b.RegisterSingleton<McpHostLifecycleService>().As<IMcpHostLifecycleService>();

        b.RegisterSingleton<EditorResourceService>().As<IEditorResourceService>();
        b.RegisterSingleton<EngineSettingsProvider>().As<IEngineSettingsProvider>();
		
        b.Register<IStorageProvider>(c =>
        {
            var window = c.Resolve<IMainWindowService>().GetMainWindow();
            return window.StorageProvider;
        });
		
        b.RegisterSingleton<WindowsFileExplorerProvider>().As<IFileExplorerProvider>();
        b.RegisterSingleton<WindowsTextEditorFileOpener>().As<ITextEditorFileOpener>();
		
        b.RegisterSingleton<ProjectTemplateProvider>().As<IProjectTemplateProvider>();
        b.RegisterSingleton<SolutionGenerator>().As<ISolutionGenerator>();

        b.RegisterSingleton<EditorSettingsService>().As<IEditorSettingsService>();
        b.RegisterSingleton<EditorStorageService>().As<IEditorStorageService>();
        b.RegisterSingleton<EditorPreferencesService>().As<IEditorPreferencesService>();
        b.RegisterSingleton<ActiveProjectService>().As<IActiveProjectService>();

        b.RegisterSingleton<ApplicationEntryPoint>();
        b.RegisterSingleton<MainWindowService>().As<IMainWindowService>();
		
        ConfigureViews(b);
    }
	
    private void ConfigureViews(ContainerBuilder b)
    {
        b.RegisterType<ShellWindowViewModel>();
    }
}
