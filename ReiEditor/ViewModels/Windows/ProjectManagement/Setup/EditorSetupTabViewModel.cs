using System;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Settings.Commands;
using ReiEditor.ViewModels.Windows.ProjectManagement.Commands;

namespace ReiEditor.ViewModels.Windows.ProjectManagement;

public class EditorSetupTabViewModel : BaseViewModel
{
	public event Action? EditorSetupEvent;
	
	public SetEngineLocationCommand SetEngineLocationCommand { get; }
	public SetMsBuildLocationCommand SetMsBuildLocationCommand { get; }
	
	public RelayCommand ConfirmCommand { get; }
	
	#region EnginePath

	private string _enginePath = "...";
	public string EnginePath
	{
		get => _enginePath;
		private set => SetField(ref _enginePath, value);
	}

	#endregion
	
	#region MsBuildPath

	private string _msBuildPath = "...";
	public string MsBuildPath
	{
		get => _msBuildPath;
		private set => SetField(ref _msBuildPath, value);
	}

	#endregion

	public EditorSetupTabValidation Validation { get; }
	
	private readonly IEditorSettingsService _editorSettingsService;
	private readonly ILogger<EditorSetupTabViewModel> _logger;

#pragma warning disable CS8618
	public EditorSetupTabViewModel() { }
#pragma warning restore CS8618

	public EditorSetupTabViewModel(
		IEditorSettingsService editorSettingsService, 
		IFactory<SetEngineLocationCommand> setEngineLocationCommand, 
		IFactory<SetMsBuildLocationCommand> setMsBuildLocationCommand, 
		ILogger<EditorSetupTabViewModel> logger)
	{
		_editorSettingsService = editorSettingsService;
		_logger = logger;
		Validation = new EditorSetupTabValidation(editorSettingsService);
		
		SetEngineLocationCommand = setEngineLocationCommand.CreateInstance();
		SetEngineLocationCommand.EnginePathSetEvent += HandleEnginePathSetEvent;

		SetMsBuildLocationCommand = setMsBuildLocationCommand.CreateInstance();
		SetMsBuildLocationCommand.MsBuildPathSetEvent += HandleMsBuildPathSetEvent;
		
		ConfirmCommand = new RelayCommand(ExecuteConfirmCommand, CanExecuteConfirmCommand);
		
		_editorSettingsService.EditorConfigurationChangedEvent += HandleEditorSettingsChangedEvent;

		EnginePath = _editorSettingsService.GetEngineLocation();
		MsBuildPath = _editorSettingsService.GetMsBuildLocation();
	}

	private void HandleEnginePathSetEvent(string path) => EnginePath = path;
	private void HandleMsBuildPathSetEvent(string path) => MsBuildPath = path;

	public override void Dispose()
	{
		base.Dispose();
		Validation.Dispose();
		_editorSettingsService.EditorConfigurationChangedEvent -= HandleEditorSettingsChangedEvent;
	}

	private void ExecuteConfirmCommand()
	{
		try
		{
			_editorSettingsService.SaveConfiguration();
			EditorSetupEvent?.Invoke();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
	}

	private bool CanExecuteConfirmCommand() => Validation.IsEditorConfigurationValid;
	private void HandleEditorSettingsChangedEvent(bool isValid) => ConfirmCommand.InvokeCanExecuteChanged();
}