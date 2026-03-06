using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.ProjectManagement.Commands;

namespace ReiEditor.ViewModels.Windows.ProjectManagement;

public class ProjectCreationTabNotifications : BaseViewModel
{
	#region ProjectNameValid

	private bool _projectNameValid = true;
	public bool ProjectNameValid
	{
		get => _projectNameValid;
		set => SetField(ref _projectNameValid, value);
	}

	#endregion
	
	#region ProjectPathValid

	private bool _projectPathValid = true;
	public bool ProjectPathValid
	{
		get => _projectPathValid;
		set => SetField(ref _projectPathValid, value);
	}

	#endregion
	
	#region ProjectCreationFailed

	private bool _projectCreationFailed;
	public bool ProjectCreationFailed
	{
		get => _projectCreationFailed;
		set => SetField(ref _projectCreationFailed, value);
	}

	#endregion

	private readonly IProjectCreationService _projectCreationService;
	private readonly CreateProjectCommand _createProjectCommand;

	public ProjectCreationTabNotifications(IProjectCreationService projectCreationService, CreateProjectCommand createProjectCommand)
	{
		_projectCreationService = projectCreationService;
		_createProjectCommand = createProjectCommand;
		
		_createProjectCommand.ExecutedCommandEvent += HandleCreateProjectCommandExecutedEvent;
		_projectCreationService.Configuration.ConfigurationChangedEvent += HandleConfigurationChangedEvent;
	}

	public override void Dispose()
	{
		base.Dispose();
		
		_createProjectCommand.ExecutedCommandEvent -= HandleCreateProjectCommandExecutedEvent;
		_projectCreationService.Configuration.ConfigurationChangedEvent -= HandleConfigurationChangedEvent;
	}

	private void HandleConfigurationChangedEvent()
	{
		ProjectNameValid = _projectCreationService.Validator.IsProjectNameValid();
		ProjectPathValid = _projectCreationService.Validator.IsProjectPathValid();
	}

	private void HandleCreateProjectCommandExecutedEvent(bool isSuccessful)
	{
		ProjectCreationFailed = !isSuccessful;
	}
}