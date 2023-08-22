using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Console;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
	public Project Project { get; }

	public ConsoleEditorWindowViewModel Console { get; } = new();

#pragma warning disable CS8618
	public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectEditorWindowViewModel(IActiveProjectService activeProjectService, ConsoleEditorWindowViewModel console)
	{
		Console = console;
		Project = activeProjectService.GetActiveProject();
	}

	public override void Dispose()
	{
		base.Dispose();
		Console.Dispose();
	}
}