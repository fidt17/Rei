using System.Threading.Tasks;
using Autofac;
using Avalonia.Platform.Storage;
using Editor.Models.App.MainWindow;
using Editor.Models.App.Shutdown;
using Editor.Models.ProjectManagement.Active;
using Editor.Models.Services.FileSystem;
using Editor.Models.Services.Logging;
using Editor.Models.Services.Preferences;
using Editor.Models.Services.Serialization;
using Editor.Models.Services.Storage;
using Editor.Startup.Common;
using Editor.Startup.EntryPoints;
using Editor.Utils.Extensions;
using Editor.Utils.Factory;
using Editor.ViewModels;

namespace Editor.Startup.Scopes;

public class ApplicationScope : BaseLifetimeScope
{
	public ApplicationScope() : base(nameof(ApplicationScope)) { }
	
	protected override async Task OnScopeStart()
	{
		await Scope.Resolve<IEditorPreferencesService>().InitializeAsync();
		
		Scope.Resolve<ApplicationEntryPoint>().Start();
	}

	protected override void ConfigureContainer(ContainerBuilder b)
	{
		b.RegisterInstance(this);
		b.RegisterSingleton<ApplicationShutdownService>().As<IApplicationShutdownService>();
		b.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));
		b.RegisterGeneric(typeof(Factory<>)).As(typeof(IFactory<>));
		b.RegisterSingleton<JsonSerializer>().As<ISerializer>();

		b.Register<IStorageProvider>(c =>
		{
			var window = c.Resolve<IMainWindowService>().GetMainWindow();
			return window.StorageProvider;
		});
		
		b.RegisterSingleton<WindowsFileExplorerProvider>().As<IFileExplorerProvider>();
		
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