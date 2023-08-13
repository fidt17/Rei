using System.Collections.Generic;
using Editor.Models.ProjectManagement;
using Editor.Startup.Common;

namespace Editor.Models.Services.Preferences;

public interface IEditorPreferencesService : IAsyncInitializable
{
	IEnumerable<Project> GetBookmarkedProjects();
	void SetBookmarkedProjects(IEnumerable<Project> paths);
}