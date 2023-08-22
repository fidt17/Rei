using System;

namespace ReiEditor.Models.Services.Logging;

public class LogMessage
{
	public LogScopeEnum Scope { get; }
	public LogLevelEnum Level { get; }
	public DateTime Time { get; }
	public string Message { get; }
	public string Details { get; }

	public LogMessage(LogScopeEnum scope, LogLevelEnum level, DateTime time, string message, string details)
	{
		Scope = scope;
		Level = level;
		Time = time;
		Message = message;
		Details = details;
	}
}