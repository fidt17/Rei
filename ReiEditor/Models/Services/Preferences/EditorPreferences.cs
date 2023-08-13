using System.Collections.Generic;

namespace ReiEditor.Models.Services.Preferences;

public class EditorPreferences
{
	public List<string> BookmarkedProjectsPaths { get; set; } = new();
}