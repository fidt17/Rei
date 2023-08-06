using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Editor.Models.Services.Logging;

namespace Editor.Models.ProjectManagement.Creation;

public class ProjectCreationService : IProjectCreationService
{
	public event Action? ProjectCreationSucceededEvent;
	public event Action? ProjectCreationFailedEvent;
	
	public ProjectCreationValidator Validator { get; }
	public ProjectCreationConfiguration Configuration { get; }

	private readonly ILogger<ProjectCreationService> _logger;
	private readonly ProjectSetup _projectSetup;

	public ProjectCreationService(IStorageProvider storageProvider, ILogger<ProjectCreationService> logger, ProjectSetup projectSetup)
	{
		_logger = logger;
		_projectSetup = projectSetup;
		Configuration = ProjectCreationUtils.GetDefaultProjectCreationConfiguration(storageProvider);
		Validator = new ProjectCreationValidator(Configuration);
	}

	public Task<bool> CreateProject()
	{
		try
		{
			_projectSetup.CreateNewProject(Configuration);
			
			_logger.LogAttention("Project creation succeeded");
			ProjectCreationSucceededEvent?.Invoke();
			return Task.FromResult(true);
		}
		catch (Exception e)
		{
			_logger.LogError(e.Message);
		}
		
		_logger.LogError("Project creation failed");
		ProjectCreationFailedEvent?.Invoke();
		return Task.FromResult(false);
	}
}