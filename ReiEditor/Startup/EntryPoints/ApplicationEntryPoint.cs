using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.App.Shutdown;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Startup.Scopes;

namespace ReiEditor.Startup.EntryPoints;

public class ApplicationEntryPoint : IDisposable
{
	private ProjectManagementScope? _projectManagementScope;
	
	private readonly ApplicationScope _scope;
	private readonly ILogger<ApplicationEntryPoint> _logger;
	private readonly IApplicationShutdownService _shutdownService;
	private readonly IActiveProjectService _activeProjectService;

	public ApplicationEntryPoint(
		ApplicationScope scope, 
		ILogger<ApplicationEntryPoint> logger, 
		IApplicationShutdownService shutdownService,
		IActiveProjectService activeProjectService)
	{
		_scope = scope;
		_logger = logger;
		_shutdownService = shutdownService;
		_activeProjectService = activeProjectService;

		_shutdownService.AddShutdownTask(_scope.StopAsync);
		
		_activeProjectService.ProjectChangedEvent += HandleProjectChangedEvent;
	}

	public void Dispose()
	{
		_activeProjectService.ProjectChangedEvent -= HandleProjectChangedEvent;
	}

	public void Start()
	{
		_logger.Log("Start");
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			try
			{
				await EnterProjectManagementScope();
			}
			catch (Exception e)
			{
				_logger.LogException(e);
				_shutdownService.Shutdown(-1);
			}
		});
	}

	private async Task EnterProjectManagementScope()
	{
		_logger.Log("Enter project management scope");
		
		try
		{
			_projectManagementScope = new ProjectManagementScope(_scope);
			await _projectManagementScope.StartAsync();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}

	private async Task EnterProjectEditorScope()
	{
		_logger.Log("Enter project editor scope");

		try
		{
			var editorScope = new EditorScope(_scope);
			await editorScope.StartAsync();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			throw;
		}
	}

	private void HandleProjectChangedEvent(Project project)
	{
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			try
			{
				if (_projectManagementScope != null)
				{
					await _projectManagementScope.StopAsync();
				}

				await EnterProjectEditorScope();
			}
			catch (Exception e)
			{
				_logger.LogException(e);
				_shutdownService.Shutdown(-1);
			}
		});
	}
}