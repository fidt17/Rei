using System.Collections.Generic;
using ReiEditor.Models.EditorApp.ViewportGrid;

namespace ReiEditor.Models.Services.Preferences;

public class EditorPreferences
{
	public string EnginePath { get; set; } = "";
	public string MsBuildPath { get; set; } = "";
	public List<string> BookmarkedProjectsPaths { get; set; } = new();

	public ConsolePreferences ConsolePreferences { get; set; } = new();
	public ViewportGridSettings GridSettings { get; set; } = new();
}