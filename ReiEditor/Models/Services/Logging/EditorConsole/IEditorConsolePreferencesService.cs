namespace ReiEditor.Models.Services.Logging.EditorConsole;

public interface IEditorConsolePreferencesService
{
	bool InfoEnabled();
	bool WarningEnabled();
	bool ErrorEnabled();

	void SetInfo(bool enabled);
	void SetWarning(bool enabled);
	void SetError(bool enabled);
}