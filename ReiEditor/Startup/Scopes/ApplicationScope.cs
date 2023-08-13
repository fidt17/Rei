using System.Threading.Tasks;
using Autofac;
using Avalonia.Platform.Storage;
using ReiEditor.Models.App.MainWindow;
using ReiEditor.Models.App.Shutdown;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Models.Services.Storage;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
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