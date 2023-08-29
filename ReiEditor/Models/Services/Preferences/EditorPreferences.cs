using System.Collections.Generic;

namespace ReiEditor.Models.Services.Preferences;

public class EditorPreferences
{
	public string EnginePath { get; set; } = "";
	public string MsBuildPath { get; set; } = "";
	public List<string> BookmarkedProjectsPaths { get; set; } = new();
}