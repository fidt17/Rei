using System.Collections.Generic;

namespace Editor.Models.Services.Preferences;

public class EditorPreferences
{
	public List<string> BookmarkedProjectsPaths { get; set; } = new();
}