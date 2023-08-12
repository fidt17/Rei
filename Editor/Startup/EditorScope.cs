using Autofac;
using Avalonia.Platform.Storage;
using Editor.Models.ProjectManagement.BookmarkedProjects;
using Editor.Models.ProjectManagement.Creation;
using Editor.Models.ProjectManagement.Deletion;
using Editor.Models.Services.FileSystem;
using Editor.Models.Services.Logging;
using Editor.Models.Services.Preferences;
using Editor.Models.Services.Serialization;
using Editor.Models.Services.Storage;
using Editor.Utils.Extensions;
using Editor.Utils.Factory;
using Editor.ViewModels;
using Editor.ViewModels.Commands;
using MainWindow = Editor.Views.MainWindow;

namespace Editor.Startup;

public class EditorScope : BaseLifetimeScope
{
	protected override void ConfigureContainer(ContainerBuilder b)
	{
		b.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));
		b.RegisterGeneric(typeof(Factory<>)).As(typeof(IFactory<>));
		b.RegisterSingleton<JsonSerializer>().As<ISerializer>();

		b.Register<IStorageProvider>(c => c.Resolve<MainWindow>().StorageProvider);
		b.RegisterSingleton<WindowsFileExplorerProvider>().As<IFileExplorerProvider>();
		
		b.RegisterSingleton<EditorStorageService>().As<IEditorStorageService>();
		b.RegisterSingleton<EditorPreferencesService>().As<IEditorPreferencesService>();

		b.RegisterType<ProjectCreationService>().As<IProjectCreationService>();
		b.RegisterSingleton<ProjectDeletionService>().As<IProjectDeletionService>();
		b.RegisterSingleton<BookmarkedProjectsService>().As<IBookmarkedProjectsService>();

		b.RegisterSingleton<EditorEntryPoint>();
		
		ConfigureViews(b);
	}
	
	private static void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterSingleton<MainWindowViewModel>();
		b.RegisterSingleton<MainWindow>();

		b.RegisterType<ProjectManagementWindowViewModel>();
		b.RegisterType<ProjectsListTabViewModel>();
		b.RegisterType<ProjectCreationTabViewModel>();
		b.RegisterType<OpenProjectCommand>();

		b.RegisterType<ProjectsListElementViewModel>();
	}
}