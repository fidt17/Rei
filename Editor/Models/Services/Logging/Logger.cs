using System;

namespace Editor.Models.Services.Logging;

public class Logger<T> : ILogger<T>
{
	public void Log(string message) => WriteToConsole(message);

	public void LogAttention(string message) => WriteToConsole(message, ConsoleColor.Cyan);

	public void LogWarning(string message) => WriteToConsole(message, ConsoleColor.Yellow);

	public void LogError(string message) => WriteToConsole(message, ConsoleColor.Red);

	private static void WriteToConsole(string message, ConsoleColor color = ConsoleColor.White)
	{
		Console.ForegroundColor = color;
		Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}][{typeof(T).Name}]: {message}");
		Console.ForegroundColor = ConsoleColor.White;
	}
}