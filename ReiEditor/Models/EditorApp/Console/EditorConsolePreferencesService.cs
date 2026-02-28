using ReiEditor.Models.Services.Preferences;

namespace ReiEditor.Models.EditorApp.Console;

public class EditorConsolePreferencesService : IEditorConsolePreferencesService
{
	private readonly ConsolePreferences _consolePreferences;
	private readonly IEditorPreferencesService _preferencesService;

	public EditorConsolePreferencesService(IEditorPreferencesService preferencesService)
	{
		_preferencesService = preferencesService;
		_consolePreferences = _preferencesService.GetConsolePreferences();
	}

	public bool DebugEnabled() => _consolePreferences.DisplayDebugLogs;
	public bool InfoEnabled() => _consolePreferences.DisplayInfoLogs;
	public bool WarningEnabled() => _consolePreferences.DisplayWarningLogs;
	public bool ErrorEnabled() => _consolePreferences.DisplayErrorLogs;

	public void SetDebug(bool enabled)
	{
		if (_consolePreferences.DisplayDebugLogs == enabled) return;

		_consolePreferences.DisplayDebugLogs = enabled;
		_preferencesService.SetConsolePreferences(_consolePreferences);
	}

	public void SetInfo(bool enabled)
	{
		if (_consolePreferences.DisplayInfoLogs == enabled) return;
		
		_consolePreferences.DisplayInfoLogs = enabled;
		_preferencesService.SetConsolePreferences(_consolePreferences);
	}

	public void SetWarning(bool enabled)
	{
		if (_consolePreferences.DisplayWarningLogs == enabled) return;
		
		_consolePreferences.DisplayWarningLogs = enabled;
		_preferencesService.SetConsolePreferences(_consolePreferences);
	}

	public void SetError(bool enabled)
	{
		if (_consolePreferences.DisplayErrorLogs == enabled) return;
		
		_consolePreferences.DisplayErrorLogs = enabled;
		_preferencesService.SetConsolePreferences(_consolePreferences);
	}
}
