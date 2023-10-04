using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Console;
using ReiEditor.ViewModels.Windows.Editor.Hierarchy;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.StatusBar;
using ReiEditor.Views.Windows.Editor.Commands;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
	public SaveProjectCommand SaveProjectCommand { get; }
	public BuildProjectCommand BuildProjectCommand { get; }
	public OpenSettingsWindowCommand OpenSettingsCommand { get; }
	
	public PlaymodePanelViewModel PlaymodePanel { get; } = new();
	public ConsoleEditorWindowViewModel Console { get; } = new();
	public HierarchyWindowViewModel Hierarchy { get; } = new();
	public StatusBarViewModel StatusBar { get; } = new();

#pragma warning disable CS8618
	public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectEditorWindowViewModel(
		IFactory<PlaymodePanelViewModel> playmodePanel,
		IFactory<ConsoleEditorWindowViewModel> console,
		IFactory<BuildProjectCommand> buildProjectCommandFactory,
		IFactory<OpenSettingsWindowCommand> openSettingsCommandFactory,
		IFactory<StatusBarViewModel> statusBarViewModelFactory,
		IFactory<HierarchyWindowViewModel> hierarchy,
		IFactory<SaveProjectCommand> saveProjectCommand)
	{
		SaveProjectCommand = saveProjectCommand.CreateInstance();
		BuildProjectCommand = buildProjectCommandFactory.CreateInstance();
		OpenSettingsCommand = openSettingsCommandFactory.CreateInstance();
		
		PlaymodePanel = playmodePanel.CreateInstance();
		Console = console.CreateInstance();
		StatusBar = statusBarViewModelFactory.CreateInstance();
		Hierarchy = hierarchy.CreateInstance();
	}

	public override void Dispose()
	{
		base.Dispose();
		PlaymodePanel.Dispose();
		Console.Dispose();
		BuildProjectCommand.Dispose();
		OpenSettingsCommand.Dispose();
		StatusBar.Dispose();
		Hierarchy.Dispose();
	}
}