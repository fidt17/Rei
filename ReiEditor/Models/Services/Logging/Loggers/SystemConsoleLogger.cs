using System;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Logging.Loggers;

public class SystemConsoleLogger<T> : ILogger<T>
{
	private readonly string _name;

	public SystemConsoleLogger(string? name = null)
	{
		_name = name ?? typeof(T).ExpandTypeName();
	}

	public void AddEmptyLine() => Console.WriteLine("");

	public void Log(string message) => WriteToConsole(message);

	public void LogWarning(string message) => WriteToConsole(message, ConsoleColor.Yellow);

	public void LogError(string message) => WriteToConsole(message, ConsoleColor.Red, 1, 1);

	public void LogException(Exception exception) => LogError("Exception: " + exception);

	private void WriteToConsole(string message, ConsoleColor color = ConsoleColor.White, int emptyLinesBefore = 0, int emptyLinesAfter = 0)
	{
		PrintEmptyLines(emptyLinesBefore);
		
		Console.ForegroundColor = color;
		Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}][{_name}]: {message}");
		Console.ForegroundColor = ConsoleColor.White;
		
		PrintEmptyLines(emptyLinesAfter);
	}

	private void PrintEmptyLines(int count)
	{
		for (int i = 0; i < count; i++)
		{
			Console.WriteLine();
		}
	}
}