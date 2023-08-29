using System;
using System.Diagnostics;
using ReiEditor.Models.Services.Logging.EditorConsole;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Logging.Loggers;

public class EditorConsoleLogger<T> : ILogger<T>
{
	private readonly IEditorConsoleService _editorConsoleService;
	private readonly SystemConsoleLogger<T> _systemConsoleLogger;

	private readonly string _name;

	public EditorConsoleLogger(IEditorConsoleService editorConsoleService, SystemConsoleLogger<T> systemConsoleLogger)
	{
		_editorConsoleService = editorConsoleService;
		_systemConsoleLogger = systemConsoleLogger;
		_name = typeof(T).ExpandTypeName();
	}

	public void Log(string message)
	{
		_systemConsoleLogger.Log(message);
		_editorConsoleService.Log(new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Info, DateTime.Now, message, FormStackTrace()));
	}

	public void LogWarning(string message)
	{
		_systemConsoleLogger.LogWarning(message);
		_editorConsoleService.Log(new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Warning, DateTime.Now, message, FormStackTrace()));
	}

	public void LogError(string message)
	{
		_systemConsoleLogger.LogError(message);
		_editorConsoleService.Log(new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Error, DateTime.Now, message, FormStackTrace()));
	}

	public void LogException(Exception exception)
	{
		_systemConsoleLogger.LogException(exception);
		_editorConsoleService.Log(new LogMessage(LogScopeEnum.Editor, LogLevelEnum.Error, DateTime.Now, exception.ToString(), FormStackTrace()));
	}

	private string FormStackTrace()
	{
		return $"{_name}" +
		       $"\nStack Trace" +
		       $"\n{new StackTrace()}\n";
	}
}