using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Console;
using ReiEditor.ViewModels.Windows.Editor.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
	public Project Project { get; }

	public PlaymodePanelViewModel PlaymodePanel { get; } = new();
	public ConsoleEditorWindowViewModel Console { get; } = new();

#pragma warning disable CS8618
	public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectEditorWindowViewModel(
		IActiveProjectService activeProjectService, 
		IFactory<PlaymodePanelViewModel> playmodePanel,
		IFactory<ConsoleEditorWindowViewModel> console)
	{
		PlaymodePanel = playmodePanel.CreateInstance();
		Console = console.CreateInstance();
		Project = activeProjectService.GetActiveProject();
	}

	public override void Dispose()
	{
		base.Dispose();
		PlaymodePanel.Dispose();
		Console.Dispose();
	}
}