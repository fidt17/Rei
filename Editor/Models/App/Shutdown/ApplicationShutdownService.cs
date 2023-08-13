using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Editor.Models.Services.Logging;

namespace Editor.Models.App.Shutdown;

public class ApplicationShutdownService : IApplicationShutdownService
{
	private bool _shutdownInProcess;
	
	private readonly ILogger<ApplicationShutdownService> _logger;
	private readonly List<Func<Task>> _shutdownTasks = new();

	public ApplicationShutdownService(ILogger<ApplicationShutdownService> logger)
	{
		_logger = logger;
	}

	public void Shutdown(int exitCode)
	{
		if (_shutdownInProcess) return;
		_shutdownInProcess = true;
		
		_logger.LogAttention($"Application shutdown... Exit code {exitCode}");
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			await ShutdownAsync(exitCode);
		});
	}

	public void AddShutdownTask(Func<Task> shutdownTask) => _shutdownTasks.Add(shutdownTask);

	private async Task ShutdownAsync(int exitCode)
	{
		try
		{
			foreach (var shutdownTask in _shutdownTasks)
			{
				await shutdownTask();
			}
			
			if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.Shutdown(exitCode);
			}
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
	}
}