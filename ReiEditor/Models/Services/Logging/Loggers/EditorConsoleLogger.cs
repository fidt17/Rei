using System;

namespace ReiEditor.Models.Services.Logging.Loggers;

public class EditorConsoleLogger<T> : ILogger<T>
{
	private readonly IEditorConsoleService _editorConsoleService;
	private readonly SystemConsoleLogger<T> _systemConsoleLogger;

	public EditorConsoleLogger(IEditorConsoleService editorConsoleService, SystemConsoleLogger<T> systemConsoleLogger)
	{
		_editorConsoleService = editorConsoleService;
		_systemConsoleLogger = systemConsoleLogger;
	}

	public void Log(string message)
	{
		_systemConsoleLogger.Log(message);
		_editorConsoleService.Log(new LogMessage(LogLevelEnum.Info, DateTime.Now, message));
	}

	public void LogWarning(string message)
	{
		_systemConsoleLogger.LogWarning(message);
		_editorConsoleService.Log(new LogMessage(LogLevelEnum.Warning, DateTime.Now, message));
	}

	public void LogError(string message)
	{
		_systemConsoleLogger.LogError(message);
		_editorConsoleService.Log(new LogMessage(LogLevelEnum.Error, DateTime.Now, message));
	}

	public void LogException(Exception exception)
	{
		_systemConsoleLogger.LogException(exception);
		_editorConsoleService.Log(new LogMessage(LogLevelEnum.Error, DateTime.Now, exception.Message));
	}
}