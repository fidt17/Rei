using System;
using System.Threading.Tasks;
using Autofac;

namespace Editor.Startup;

public abstract class BaseLifetimeScope : IAsyncDisposable
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

	protected abstract void ConfigureContainer(ContainerBuilder containerBuilder);
}