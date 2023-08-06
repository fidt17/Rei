using System;
using System.IO;
using Editor.Models.Serialization;
using Editor.Models.Services.Logging;

namespace Editor.Models.ProjectManagement.Creation;

public class ProjectSetup
{
	private readonly ILogger<ProjectSetup> _logger;
	private readonly ISerializer<Project> _projectSerializer;

	public ProjectSetup(ILogger<ProjectSetup> logger, ISerializer<Project> projectSerializer)
	{
		_logger = logger;
		_projectSerializer = projectSerializer;
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
		File.WriteAllText(projectFilePath, _projectSerializer.Serialize(project));
	}
}