using System;
using System.Windows.Input;
using Avalonia.Threading;
using Editor.Models.ProjectManagement.Creation;

namespace Editor.ViewModels.Commands;

public class CreateProjectCommand : BaseViewModel, ICommand
{
	public event Action<bool>? ExecutedCommandEvent;
	public event EventHandler? CanExecuteChanged;
	
	private bool _configurationValid;
	private bool _isProjectCreationInProgress;
	private readonly IProjectCreationService _projectCreationService;

	public CreateProjectCommand(IProjectCreationService projectCreationService)
	{
		_projectCreationService = projectCreationService;
		_projectCreationService.Configuration.ConfigurationChangedEvent += HandleConfigurationChangedEvent;
		_configurationValid = _projectCreationService.Validator.IsConfigurationValid();
	}

	public override void Dispose()
	{
		base.Dispose();
		_projectCreationService.Configuration.ConfigurationChangedEvent -= HandleConfigurationChangedEvent;
	}

	private void HandleConfigurationChangedEvent()
	{
		var isConfigurationValid = _projectCreationService.Validator.IsConfigurationValid();
		if (isConfigurationValid != _configurationValid)
		{
			_configurationValid = isConfigurationValid;
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public bool CanExecute(object? parameter) => _configurationValid && !_isProjectCreationInProgress;

	public void Execute(object? parameter)
	{
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			_isProjectCreationInProgress = true;
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
			
			var project = await _projectCreationService.CreateProject();
			var didCreate = project != null;
			
			_isProjectCreationInProgress = false;
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
			
			ExecutedCommandEvent?.Invoke(didCreate);
		});
	}
}