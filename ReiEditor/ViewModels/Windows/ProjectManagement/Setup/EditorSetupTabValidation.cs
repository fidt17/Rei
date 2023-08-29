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
	
	#region IsMsBuildPathValid

	private bool _isMsBuildPathValid;
	public bool IsMsBuildPathValid
	{
		get => _isMsBuildPathValid;
		private set => SetField(ref _isMsBuildPathValid, value);
	}

	#endregion

	private readonly IEditorSettingsService _editorSettingsService;

	public EditorSetupTabValidation(IEditorSettingsService editorSettingsService)
	{
		_editorSettingsService = editorSettingsService;

		IsEditorConfigurationValid = _editorSettingsService.IsEditorConfigurationValid();
		IsEnginePathValid = _editorSettingsService.IsEngineLocationValid();
		IsMsBuildPathValid = _editorSettingsService.IsMsBuildLocationValid();
		
		_editorSettingsService.EditorConfigurationChangedEvent += HandleEditorSettingsChangedEvent;
	}

	public override void Dispose()
	{
		base.Dispose();
		_editorSettingsService.EditorConfigurationChangedEvent -= HandleEditorSettingsChangedEvent;
	}

	private void HandleEditorSettingsChangedEvent(bool isValid)
	{
		IsEditorConfigurationValid = isValid;
		IsEnginePathValid = _editorSettingsService.IsEngineLocationValid();
		IsMsBuildPathValid = _editorSettingsService.IsMsBuildLocationValid();
	}
}