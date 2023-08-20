using System;

namespace ReiEditor.Models.Services.Logging.Loggers;

// ReSharper disable once UnusedTypeParameter
public interface ILogger<T>
{
	void Log(string message);
	void LogAttention(string message);
	void LogWarning(string message);
	void LogError(string message);
	void LogException(Exception exception);
}