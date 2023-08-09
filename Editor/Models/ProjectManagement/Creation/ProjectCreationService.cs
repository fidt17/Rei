using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Editor.Models.Services.Logging;

namespace Editor.Models.ProjectManagement.Creation;

public class ProjectCreationService : IProjectCreationService
{
	public event Action<Project>? ProjectCreatedEvent;
	public event Action? ProjectCreationFailedEvent;
	
	public ProjectCreationValidator Validator { get; }
	public ProjectCreationConfiguration Configuration { get; }

	private readonly ILogger<ProjectCreationService> _logger;
	private readonly ProjectSetupUtility _projectSetupUtility;

	public ProjectCreationService(IStorageProvider storageProvider, ILogger<ProjectCreationService> logger, ProjectSetupUtility projectSetupUtility)
	{
		_logger = logger;
		
		_projectSetupUtility = projectSetupUtility;
		Configuration = ProjectCreationUtils.GetDefaultProjectCreationConfiguration(storageProvider);
		Validator = new ProjectCreationValidator(Configuration);
	}

	public Task<Project?> CreateProject()
	{
		try
		{
			var project = _projectSetupUtility.CreateNewProject(Configuration);
			
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
}