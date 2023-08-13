using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Editor.Models.Services.App.Shutdown;
using Editor.Models.Services.Logging;
using Editor.Startup.Scopes;

namespace Editor.Startup.EntryPoints;

public class ApplicationEntryPoint
{
	private readonly ApplicationScope _scope;
	private readonly ILogger<ApplicationEntryPoint> _logger;
	private readonly IApplicationShutdownService _shutdownService;

	public ApplicationEntryPoint(ApplicationScope scope, ILogger<ApplicationEntryPoint> logger, IApplicationShutdownService shutdownService)
	{
		_scope = scope;
		_logger = logger;
		_shutdownService = shutdownService;
		
		_shutdownService.AddShutdownTask(_scope.StopAsync);
	}

	public void Start()
	{
		_logger.Log("Start");
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			try
			{
				await OpenProjectManagementWindow();
			}
			catch (Exception e)
			{
				_logger.LogException(e);
				_shutdownService.Shutdown(-1);
			}
		});
	}

	private async Task OpenProjectManagementWindow()
	{
		_logger.Log("Start project management scope");
		try
		{
			var projectManagementScope = new ProjectManagementScope(_scope);
			await projectManagementScope.StartAsync();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}
}