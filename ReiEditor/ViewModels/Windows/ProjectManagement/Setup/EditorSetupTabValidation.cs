using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.ProjectManagement;

public class EditorSetupTabValidation : BaseViewModel
{
	#region IsEditorConfigurationValid

	private bool _isEditorConfigurationValid;
	public bool IsEditorConfigurationValid
	{
		get => _isEditorConfigurationValid;
		private set => SetField(ref _isEditorConfigurationValid, value);
	}

	#endregion
	
	#region IsEnginePathValid

	private bool _isEnginePathValid;
	public bool IsEnginePathValid
	{
		get => _isEnginePathValid;
		private set => SetField(ref _isEnginePathValid, value);
	}

	#endregion

	private readonly IEditorConfigurationService _editorConfigurationService;

	public EditorSetupTabValidation(IEditorConfigurationService editorConfigurationService)
	{
		_editorConfigurationService = editorConfigurationService;

		IsEditorConfigurationValid = _editorConfigurationService.IsEditorConfigurationValid();
		IsEnginePathValid = _editorConfigurationService.IsEngineLocationValid();
		
		_editorConfigurationService.EditorConfigurationChangedEvent += HandleEditorConfigurationChangedEvent;
	}

	public override void Dispose()
	{
		base.Dispose();
		_editorConfigurationService.EditorConfigurationChangedEvent -= HandleEditorConfigurationChangedEvent;
	}

	private void HandleEditorConfigurationChangedEvent(bool isValid)
	{
		IsEditorConfigurationValid = isValid;
		IsEnginePathValid = _editorConfigurationService.IsEngineLocationValid();
	}
}