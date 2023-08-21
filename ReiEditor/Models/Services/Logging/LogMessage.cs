using System;

namespace ReiEditor.Models.Services.Logging;

public class LogMessage
{
	public LogLevelEnum LogLevel { get; }
	public DateTime Time { get; }
	public string Message { get; }
	public string Details { get; }

	public LogMessage(LogLevelEnum logLevel, DateTime time, string message, string details)
	{
		LogLevel = logLevel;
		Time = time;
		Message = message;
		Details = details;
	}
}