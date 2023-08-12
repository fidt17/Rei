using System;
using System.IO;
using Editor.Models.Services.Logging;

namespace Editor.Models.ProjectManagement.Deletion;

public class ProjectDeletionService : IProjectDeletionService
{
	private readonly ILogger<ProjectDeletionService> _logger;

	public ProjectDeletionService(ILogger<ProjectDeletionService> logger)
	{
		_logger = logger;
	}

	public void DeleteProject(Project project)
	{
		try
		{
			_logger.Log($"Delete project [{project.ProjectName}]");
			Directory.Delete(project.GetDirectoryPath(), recursive: true);
			_logger.Log($"Deleted project [{project.ProjectName}]");
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
	}
}