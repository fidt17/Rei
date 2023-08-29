using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Console;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.Views.Windows.Editor.Build.Commands;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
	public BuildProjectCommand BuildProjectCommand { get; }
	public OpenSettingsWindowCommand OpenSettingsCommand { get; }
	
	public Project Project { get; }

	public PlaymodePanelViewModel PlaymodePanel { get; } = new();
	
	public ConsoleEditorWindowViewModel Console { get; } = new();

#pragma warning disable CS8618
	public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectEditorWindowViewModel(
		IActiveProjectService activeProjectService, 
		IFactory<PlaymodePanelViewModel> playmodePanel,
		IFactory<ConsoleEditorWindowViewModel> console,
		IFactory<BuildProjectCommand> buildProjectCommandFactory,
		IFactory<OpenSettingsWindowCommand> openSettingsCommandFactory)
	{
		PlaymodePanel = playmodePanel.CreateInstance();
		Console = console.CreateInstance();
		Project = activeProjectService.GetActiveProject();
		BuildProjectCommand = buildProjectCommandFactory.CreateInstance();
		OpenSettingsCommand = openSettingsCommandFactory.CreateInstance();
	}

	public override void Dispose()
	{
		base.Dispose();
		PlaymodePanel.Dispose();
		Console.Dispose();
		BuildProjectCommand.Dispose();
		OpenSettingsCommand.Dispose();
	}
}