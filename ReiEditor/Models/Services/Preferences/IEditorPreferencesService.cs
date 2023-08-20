using System.Collections.Generic;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Startup.Common;

namespace ReiEditor.Models.Services.Preferences;

public interface IEditorPreferencesService : IAsyncInitializable
{
	string? GetEnginePath();
	void SetEnginePath(string path);
	
	IEnumerable<Project> GetBookmarkedProjects();
	void SetBookmarkedProjects(IEnumerable<Project> paths);
}