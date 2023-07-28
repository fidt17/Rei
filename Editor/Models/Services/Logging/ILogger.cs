namespace Editor.Models.Services.Logging;

// ReSharper disable once UnusedTypeParameter
public interface ILogger<T>
{
	void Log(string message);
	void LogWarn(string message);
	void LogErr(string message);
}