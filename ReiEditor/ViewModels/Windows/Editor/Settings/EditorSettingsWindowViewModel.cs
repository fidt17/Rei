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
	public SetMsBuildLocationCommand SetMsBuildLocationCommand { get; }
	
	#region MsBuildPath

	private string _msBuildPath = "...";
	public string MsBuildPath
	{
		get => _msBuildPath;
		private set => SetField(ref _msBuildPath, value);
	}

	#endregion
	
	public EditorSettingsValidation Validation { get; }

#pragma warning disable CS8618
	public EditorSettingsWindowViewModel() { }
#pragma warning restore CS8618

	public EditorSettingsWindowViewModel(IEditorSettingsService editorSettingsService, 
		ISettingsWindowService settingsWindowService,
		IFactory<SetMsBuildLocationCommand> setMsBuildLocationCommandFactory)
	{
		Validation = new EditorSettingsValidation(editorSettingsService);
		
		SetMsBuildLocationCommand = setMsBuildLocationCommandFactory.CreateInstance();
		SetMsBuildLocationCommand.MsBuildPathSetEvent += HandleMsBuildPathSetEvent;

		ConfirmCommand = new RelayCommand(() =>
		{
			editorSettingsService.SaveConfiguration();
			settingsWindowService.CloseSettingsWindow();
		});

		MsBuildPath = editorSettingsService.GetMsBuildLocation();
	}

	public override void Dispose()
	{
		Validation.Dispose();
	}

	private void HandleMsBuildPathSetEvent(string path) => MsBuildPath = path;
}