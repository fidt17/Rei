using System;

namespace Editor.Models.Services.Logging;

// ReSharper disable once UnusedTypeParameter
public interface ILogger<T>
{
	void Log(string message);
	void LogAttention(string message);
	void LogWarning(string message);
	void LogError(string message);
	void LogException(Exception exception);
}