using System;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.ProjectManagement.Active;

public class ActiveProjectService : IActiveProjectService
{
	public event Action<Project>? ProjectChangedEvent;

	private Project? _project;

	private readonly ILogger<ActiveProjectService> _logger;

	public ActiveProjectService(ILogger<ActiveProjectService> logger)
	{
		_logger = logger;
	}

	public Project GetActiveProject()
	{
		return _project ?? throw new NullReferenceException(nameof(_project));
	}

	public void OpenProject(Project project)
	{
		if (_project == project)
		{
			_logger.LogWarning($"Project {project.ProjectName} is already active");
		}

		_project = project;
		ProjectChangedEvent?.Invoke(_project);
	}
}