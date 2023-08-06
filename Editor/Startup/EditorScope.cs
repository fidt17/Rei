using System;
using System.Threading.Tasks;
using Autofac;
using Avalonia.Platform.Storage;
using Editor.Models.ProjectManagement.Creation;
using Editor.Models.Serialization;
using Editor.Models.Services.Logging;
using Editor.Utils.Extensions;
using Editor.Utils.Factory;
using Editor.ViewModels;
using MainWindow = Editor.Views.MainWindow;

namespace Editor.Startup;

public class EditorScope : IAsyncDisposable
{
	private IContainer _container = null!;
	
	public IContainer Configure()
	{
		var containerBuilder = new ContainerBuilder();
		ConfigureContainer(containerBuilder);
		_container = containerBuilder.Build();
		
		return _container;
	}

	public async ValueTask DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	private static void ConfigureContainer(ContainerBuilder b)
	{
		b.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));
		b.RegisterGeneric(typeof(Factory<>)).As(typeof(IFactory<>));
		b.RegisterGeneric(typeof(JsonSerializer<>)).As(typeof(ISerializer<>));

		b.Register<IStorageProvider>(c => c.Resolve<MainWindow>().StorageProvider);

		b.RegisterType<ProjectCreationService>().As<IProjectCreationService>();
		b.RegisterSingleton<ProjectSetup>();

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
	}
}