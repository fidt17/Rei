namespace ReiEditor.Models.EditorApp.Console;

public interface IEditorConsolePreferencesService
{
	bool DebugEnabled();
	bool InfoEnabled();
	bool WarningEnabled();
	bool ErrorEnabled();

	void SetDebug(bool enabled);
	void SetInfo(bool enabled);
	void SetWarning(bool enabled);
	void SetError(bool enabled);
}
