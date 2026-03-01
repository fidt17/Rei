using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Settings;

public class EditorSettingsValidation : BaseViewModel
{
    #region IsMsBuildPathValid
    
    private bool _isMsBuildPathValid;
    public bool IsMsBuildPathValid
    {
        get => _isMsBuildPathValid;
        private set => SetField(ref _isMsBuildPathValid, value);
    }
    
    #endregion

    #region IsTextEditorPathValid

    private bool _isTextEditorPathValid;
    public bool IsTextEditorPathValid
    {
        get => _isTextEditorPathValid;
        private set => SetField(ref _isTextEditorPathValid, value);
    }

    #endregion
    
    private readonly IEditorSettingsService _editorSettingsService;
    
    public EditorSettingsValidation(IEditorSettingsService editorSettingsService)
    {
        _editorSettingsService = editorSettingsService;
    
        IsMsBuildPathValid = _editorSettingsService.IsMsBuildLocationValid();
        IsTextEditorPathValid = _editorSettingsService.IsTextEditorLocationValid();
    		
        _editorSettingsService.EditorConfigurationChangedEvent += HandleEditorSettingsChangedEvent;
    }
    
    public override void Dispose()
    {
        base.Dispose();
        _editorSettingsService.EditorConfigurationChangedEvent -= HandleEditorSettingsChangedEvent;
    }
    
    private void HandleEditorSettingsChangedEvent(bool isValid)
    {
        IsMsBuildPathValid = _editorSettingsService.IsMsBuildLocationValid();
        IsTextEditorPathValid = _editorSettingsService.IsTextEditorLocationValid();
    }
}