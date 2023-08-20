using System;

namespace ReiEditor.Models.Services.Logging;

public class LogMessage
{
	public LogLevelEnum LogLevel { get; }
	public DateTime Time { get; }
	public string Message { get; }

	public LogMessage(LogLevelEnum logLevel, DateTime time, string message)
	{
		LogLevel = logLevel;
		Time = time;
		Message = message;
	}
}