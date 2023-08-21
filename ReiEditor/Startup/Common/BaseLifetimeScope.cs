using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Startup.Common;

public abstract class BaseLifetimeScope
{
	public readonly ILifetimeScope Scope;

	private readonly BaseLifetimeScope? _parentScope;
	private readonly List<BaseLifetimeScope> _childScopes = new();
	private readonly ILogger<BaseLifetimeScope> _logger;

	protected BaseLifetimeScope(string scopeTitle)
	{
		_logger = new SystemConsoleLogger<BaseLifetimeScope>(scopeTitle);
		
		Scope = BuildScope();
	}

	protected BaseLifetimeScope(string scopeTitle, BaseLifetimeScope parentScope)
	{
		_logger = new SystemConsoleLogger<BaseLifetimeScope>(scopeTitle);
		
		_parentScope = parentScope;
		Scope = _parentScope.Scope.BeginLifetimeScope(Configure);
		_parentScope.RegisterChildScope(this);
	}
	
	public Task StartAsync()
	{
		_logger.LogWarning("Start");

		try
		{
			return OnScopeStart();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}

	public async Task StopAsync()
	{
		_logger.LogWarning("Stop");

		try
		{
			for (var i = _childScopes.Count - 1; i >= 0; i--)
			{
				await _childScopes[i].StopAsync();
			}
			
			await Scope.DisposeAsync();
			
			_parentScope?.UnregisterChildScope(this);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}
	
	protected virtual Task OnScopeStart() => Task.CompletedTask;

	protected abstract void ConfigureContainer(ContainerBuilder containerBuilder);

	private ILifetimeScope BuildScope()
	{
		var containerBuilder = new ContainerBuilder();
		Configure(containerBuilder);
		return containerBuilder.Build();
	}

	private void Configure(ContainerBuilder containerBuilder)
	{
		_logger.LogWarning("Configure");
		ConfigureContainer(containerBuilder);
	}

	private void RegisterChildScope(BaseLifetimeScope childScope) => _childScopes.Add(childScope);
	private void UnregisterChildScope(BaseLifetimeScope childScope) => _childScopes.Remove(childScope);
}