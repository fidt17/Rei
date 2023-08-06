using Avalonia.Threading;
using Editor.Models.ProjectManagement.Creation;

namespace Editor.ViewModels;

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

	public ProjectCreationTabNotifications(IProjectCreationService projectCreationService)
	{
		_projectCreationService = projectCreationService;
		_projectCreationService.ProjectCreationFailedEvent += HandleProjectCreationFailedEvent;
		_projectCreationService.ProjectCreationSucceededEvent += HandleProjectCreationSucceededEvent;
		
		_projectCreationService.Configuration.ConfigurationChangedEvent += HandleConfigurationChangedEvent;
	}

	public override void Dispose()
	{
		base.Dispose();
		
		_projectCreationService.ProjectCreationFailedEvent -= HandleProjectCreationFailedEvent;
		_projectCreationService.ProjectCreationSucceededEvent -= HandleProjectCreationSucceededEvent;
		
		_projectCreationService.Configuration.ConfigurationChangedEvent -= HandleConfigurationChangedEvent;
	}

	private void HandleConfigurationChangedEvent()
	{
		ProjectNameValid = _projectCreationService.Validator.IsProjectNameValid();
		ProjectPathValid = _projectCreationService.Validator.IsProjectPathValid();
	}

	private void HandleProjectCreationFailedEvent()
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			ProjectCreationFailed = true;
		});
	}

	private void HandleProjectCreationSucceededEvent()
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			ProjectCreationFailed = false;
		});
	}
}