using System;

namespace Editor.Models.ProjectManagement.Active;

public interface IActiveProjectService
{
	event Action<Project> ProjectChangedEvent;

	Project GetActiveProject();
	void OpenProject(Project project);
}