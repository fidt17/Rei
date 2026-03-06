using System;

namespace ReiEditor.Models.Services.Logging.Loggers;

public interface ILogger
{
	void AddEmptyLine();
	void Log(string message);
	void LogWarning(string message);
	void LogError(string message);
	void LogException(Exception exception);
}

// ReSharper disable once UnusedTypeParameter
public interface ILogger<T> : ILogger { }