using System;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.ProjectManagement.Commands;

namespace ReiEditor.ViewModels.Windows.ProjectManagement;

public class EditorSetupTabViewModel : BaseViewModel
{
	public event Action? EditorSetupEvent;
	
	public SetEngineLocationCommand SetEngineLocationCommand { get; }
	public RelayCommand ConfirmCommand { get; }
	
	#region EnginePath

	private string _enginePath = "";
	public string EnginePath
	{
		get => _enginePath;
		private set => SetField(ref _enginePath, value);
	}

	#endregion

	public EditorSetupTabValidation Validation { get; }
	
	private readonly IEditorConfigurationService _editorConfigurationService;
	private readonly ILogger<EditorSetupTabViewModel> _logger;

#pragma warning disable CS8618
	public EditorSetupTabViewModel() { }
#pragma warning restore CS8618

	public EditorSetupTabViewModel(IEditorConfigurationService editorConfigurationService, SetEngineLocationCommand setEngineLocationCommand, ILogger<EditorSetupTabViewModel> logger)
	{
		_editorConfigurationService = editorConfigurationService;
		_logger = logger;
		Validation = new EditorSetupTabValidation(editorConfigurationService);
		
		SetEngineLocationCommand = setEngineLocationCommand;
		SetEngineLocationCommand.EnginePathSetEvent += HandleEnginePathSetEvent;
		ConfirmCommand = new RelayCommand(ExecuteConfirmCommand, CanExecuteConfirmCommand);
		
		_editorConfigurationService.EditorConfigurationChangedEvent += HandleEditorConfigurationChangedEvent;
	}

	private void HandleEnginePathSetEvent(string path) => EnginePath = path;

	public override void Dispose()
	{
		base.Dispose();
		Validation.Dispose();
		_editorConfigurationService.EditorConfigurationChangedEvent -= HandleEditorConfigurationChangedEvent;
	}

	private void ExecuteConfirmCommand()
	{
		try
		{
			_editorConfigurationService.SaveConfiguration();
			EditorSetupEvent?.Invoke();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
	}

	private bool CanExecuteConfirmCommand() => Validation.IsEditorConfigurationValid;
	private void HandleEditorConfigurationChangedEvent(bool isValid) => ConfirmCommand.InvokeCanExecuteChanged();
}