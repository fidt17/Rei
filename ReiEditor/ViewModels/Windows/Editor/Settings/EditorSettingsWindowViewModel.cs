using ReiEditor.Models.EditorApp.SettingsWindow;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Utils;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Settings.Commands;

namespace ReiEditor.ViewModels.Windows.Editor.Settings;

public class EditorSettingsWindowViewModel : BaseViewModel
{
	public RelayCommand ConfirmCommand { get; }
    public RelayCommand UseSystemDefaultTextEditorCommand { get; }
	public SetMsBuildLocationCommand SetMsBuildLocationCommand { get; }
    public SetTextEditorLocationCommand SetTextEditorLocationCommand { get; }
	
	#region MsBuildPath

	private string _msBuildPath = "...";
	public string MsBuildPath
	{
		get => _msBuildPath;
		private set => SetField(ref _msBuildPath, value);
	}

	#endregion

    #region TextEditorPath

    private string _textEditorPath = "";
    public string TextEditorPath
    {
        get => _textEditorPath;
        private set => SetField(ref _textEditorPath, value);
    }

    #endregion
	
	public EditorSettingsValidation Validation { get; }

    private readonly IEditorSettingsService _editorSettingsService;

#pragma warning disable CS8618
	public EditorSettingsWindowViewModel() { }
#pragma warning restore CS8618

	public EditorSettingsWindowViewModel(IEditorSettingsService editorSettingsService, 
		ISettingsWindowService settingsWindowService,
		IFactory<SetMsBuildLocationCommand> setMsBuildLocationCommandFactory,
        IFactory<SetTextEditorLocationCommand> setTextEditorLocationCommandFactory)
	{
        _editorSettingsService = editorSettingsService;
		Validation = new EditorSettingsValidation(editorSettingsService);
		
		SetMsBuildLocationCommand = setMsBuildLocationCommandFactory.CreateInstance();
		SetMsBuildLocationCommand.MsBuildPathSetEvent += HandleMsBuildPathSetEvent;

        SetTextEditorLocationCommand = setTextEditorLocationCommandFactory.CreateInstance();
        SetTextEditorLocationCommand.TextEditorPathSetEvent += HandleTextEditorPathSetEvent;

        UseSystemDefaultTextEditorCommand = new RelayCommand(ClearTextEditorPath);

		ConfirmCommand = new RelayCommand(() =>
		{
			editorSettingsService.SaveConfiguration();
			settingsWindowService.CloseSettingsWindow();
		});

		MsBuildPath = editorSettingsService.GetMsBuildLocation();
        TextEditorPath = editorSettingsService.GetTextEditorLocation();
	}

	public override void Dispose()
	{
        SetMsBuildLocationCommand.MsBuildPathSetEvent -= HandleMsBuildPathSetEvent;
        SetTextEditorLocationCommand.TextEditorPathSetEvent -= HandleTextEditorPathSetEvent;
		Validation.Dispose();
	}

	private void HandleMsBuildPathSetEvent(string path) => MsBuildPath = path;
    private void HandleTextEditorPathSetEvent(string path) => TextEditorPath = path;

    private void ClearTextEditorPath()
    {
        _editorSettingsService.ClearTextEditorLocation();
        TextEditorPath = _editorSettingsService.GetTextEditorLocation();
    }
}
