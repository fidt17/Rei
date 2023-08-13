using System;
using System.Linq;

namespace Editor.Models.Services.Logging;

public class Logger<T> : ILogger<T>
{
	private readonly string _name;

	public Logger(string? name = null)
	{
		_name = name ?? ExpandTypeName(typeof(T));
	}

	public void Log(string message) => WriteToConsole(message);

	public void LogAttention(string message) => WriteToConsole(message, ConsoleColor.Cyan, 1);

	public void LogWarning(string message) => WriteToConsole(message, ConsoleColor.Yellow);

	public void LogError(string message) => WriteToConsole(message, ConsoleColor.Red, 1, 1);

	public void LogException(Exception exception) => LogError(exception.Message);

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
	
	private static string ExpandTypeName(Type t)
	{
		return !t.IsGenericType || t.IsGenericTypeDefinition
			? !t.IsGenericTypeDefinition ? t.Name : t.Name.Remove(t.Name.IndexOf('`'))
			: $"{ExpandTypeName(t.GetGenericTypeDefinition())}<{string.Join(',', t.GetGenericArguments().Select(ExpandTypeName))}>";
	}
}