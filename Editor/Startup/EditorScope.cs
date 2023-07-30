using System;
using System.Threading.Tasks;
using Autofac;
using Editor.Models.Services.Logging;
using Editor.Utils.Extensions;
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
		
		b.RegisterType<EditorEntryPoint>().AsSelf().SingleInstance();
		
		ConfigureViews(b);
	}

	private static void ConfigureViews(ContainerBuilder b)
	{
		b.RegisterType<MainWindowViewModel>().SingleInstance();
		b.RegisterType<MainWindow>();
		
		b.RegisterFactory<ProjectManagementTabViewModel>();
	}
}