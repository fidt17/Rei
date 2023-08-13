using Editor.Models.ProjectManagement;
using Editor.Models.ProjectManagement.Active;

namespace Editor.ViewModels.Editor;

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