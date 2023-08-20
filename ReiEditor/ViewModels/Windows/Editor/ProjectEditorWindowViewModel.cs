using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
	public Project Project { get; }

#pragma warning disable CS8618
	public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ProjectEditorWindowViewModel(IActiveProjectService activeProjectService)
	{
		Project = activeProjectService.GetActiveProject();
	}
}