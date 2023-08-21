using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using ReiEditor.Models.ProjectManagement.Creation.Template;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.ProjectManagement.Creation;

public class ProjectCreationService : IProjectCreationService
{
	public event Action<Project>? ProjectCreatedEvent;
	public event Action? ProjectCreationFailedEvent;
	
	public ProjectCreationValidator Validator { get; }
	public ProjectCreationConfiguration Configuration { get; }

	private readonly ILogger<ProjectCreationService> _logger;
	private readonly ISerializer _serializer;
	private readonly ISolutionGenerator _solutionGenerator;

	public ProjectCreationService(IStorageProvider storageProvider, ILogger<ProjectCreationService> logger, ISerializer serializer, ISolutionGenerator solutionGenerator)
	{
		_logger = logger;
		_serializer = serializer;
		_solutionGenerator = solutionGenerator;

		Configuration = ProjectCreationUtils.GetDefaultProjectCreationConfiguration(storageProvider);
		Validator = new ProjectCreationValidator(Configuration);
	}

	public async Task<Project?> CreateProject()
	{
		try
		{
			_logger.LogWarning("Creating project");
			
			var project = await CreateFromConfiguration(Configuration);
			
			_logger.LogWarning("Project creation succeeded");
			ProjectCreatedEvent?.Invoke(project);

			return project;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
		
		_logger.LogError("Project creation failed");
		ProjectCreationFailedEvent?.Invoke();
		return null;
	}

	private async Task<Project> CreateFromConfiguration(ProjectCreationConfiguration configuration)
	{
		var project = new Project();
		project.SetProjectName(configuration.ProjectName);
		project.SetProjectLastEditTime(DateTime.UtcNow);
		
		var root = configuration.FullPath;
		
		CreateDirectoryStructure(root);
		
		var solutionPath = await CreateSolution(configuration);
		project.SetProjectSolutionPath(solutionPath);
		
		CreateProjectFile(root, project);

		return project;
	}
	
	private void CreateDirectoryStructure(string root)
	{
		_logger.Log("Creating directory structure");
		
		_logger.Log($"Creating root directory at: {root}");
		Directory.CreateDirectory(root);
	}

	private void CreateProjectFile(string root, Project project)
	{
		var projectFilePath = Path.Combine(root, $"{project.ProjectName}{FileExtensions.PROJECT_FILE_EXTENSION}");
		project.SetProjectFilePath(projectFilePath);
		
		_logger.Log($"Creating project file at {projectFilePath}");
		File.WriteAllText(projectFilePath, _serializer.Serialize(project));
	}

	private async Task<string> CreateSolution(ProjectCreationConfiguration configuration)
	{
		_logger.Log("Creating solution");
		var solutionPath = await _solutionGenerator.GenerateSolution(configuration);
		return solutionPath;
	}
}