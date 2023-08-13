using System;
using System.Collections.Generic;

namespace ReiEditor.Models.ProjectManagement.BookmarkedProjects;

public interface IBookmarkedProjectsService
{
	event Action BookmarkedProjectsCollectionChangedEvent;
	
	IEnumerable<Project> GetBookmarkedProjects();
	void AddProject(Project project);
	void RemoveProject(Project project);
}