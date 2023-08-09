using System;
using System.IO;
using Editor.Models.Services.Logging;
using Editor.Models.Services.Serialization;

namespace Editor.Models.ProjectManagement.Creation;

public class ProjectSetupUtility
{
	private readonly ILogger<ProjectSetupUtility> _logger;
	private readonly ISerializer _serializer;

	public ProjectSetupUtility(ILogger<ProjectSetupUtility> logger, ISerializer serializer)
	{
		_logger = logger;
		_serializer = serializer;
	}

	public Project CreateNewProject(ProjectCreationConfiguration configuration)
	{
		_logger.LogAttention("--- Create project ---");
		
		var project = new Project();
		project.SetProjectName(configuration.ProjectName);
		project.SetProjectLastEditTime(DateTime.UtcNow);
		
		var root = configuration.FullPath;
		CreateDirectoryStructure(root);
		CreateProjectFiles(root, project);

		return project;
	}
	
	private void CreateDirectoryStructure(string root)
	{
		_logger.Log("Create directory structure");
		
		_logger.Log($"Create root directory at: {root}");
		Directory.CreateDirectory(root);
	}

	private void CreateProjectFiles(string root, Project project)
	{
		var projectFilePath = Path.Combine(root, $"{project.ProjectName}{FileExtensions.PROJECT_FILE_EXTENSION}");
		project.SetProjectFilePath(projectFilePath);
		
		_logger.Log($"Create project file at {projectFilePath}");
		File.WriteAllText(projectFilePath, _serializer.Serialize(project));
	}
}