using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Editor.Models.Services.FileSystem;
using Editor.Models.Services.Logging;
using Editor.Models.Services.Serialization;

namespace Editor.Models.ProjectManagement.Creation;

public class ProjectCreationService : IProjectCreationService
{
	public event Action<Project>? ProjectCreatedEvent;
	public event Action? ProjectCreationFailedEvent;
	
	public ProjectCreationValidator Validator { get; }
	public ProjectCreationConfiguration Configuration { get; }

	private readonly ILogger<ProjectCreationService> _logger;
	private readonly ISerializer _serializer;

	public ProjectCreationService(IStorageProvider storageProvider, ILogger<ProjectCreationService> logger, ISerializer serializer)
	{
		_logger = logger;
		_serializer = serializer;

		Configuration = ProjectCreationUtils.GetDefaultProjectCreationConfiguration(storageProvider);
		Validator = new ProjectCreationValidator(Configuration);
	}

	public Task<Project?> CreateProject()
	{
		try
		{
			_logger.LogAttention("Create project");
			
			var project = CreateFromConfiguration(Configuration);
			
			_logger.LogAttention("Project creation succeeded");
			ProjectCreatedEvent?.Invoke(project);

			return Task.FromResult<Project?>(project);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
		
		_logger.LogError("Project creation failed");
		ProjectCreationFailedEvent?.Invoke();
		return Task.FromResult<Project?>(null);
	}

	private Project CreateFromConfiguration(ProjectCreationConfiguration configuration)
	{
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