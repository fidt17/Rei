using System;

namespace ReiEditor.Models.ProjectManagement.Active;

public interface IActiveProjectService
{
	event Action<Project> ActiveProjectChangedEvent;

	Project GetActiveProject();
	void OpenProject(Project project);
}