using System;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.ReactiveUI;

namespace ReiEditor;

class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
		{
			WriteCrashReport("AppDomain.CurrentDomain.UnhandledException", eventArgs.ExceptionObject?.ToString() ?? "(null exception object)");
		};

		try
		{
			var appBuilder = BuildAvaloniaApp();
			appBuilder.StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
		}
		catch (Exception e)
		{
			WriteCrashReport("Program.Main", e.ToString());
			Console.WriteLine($"Unhandled exception.\n{e.Message}\n{e.InnerException?.Message}");
		}
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace(LogEventLevel.Verbose)
			.UseReactiveUI();
	}

	private static void WriteCrashReport(string source, string details)
	{
		try
		{
			var root = Directory.GetCurrentDirectory();
			var crashDirectory = Path.Combine(root, "crash_reports");
			Directory.CreateDirectory(crashDirectory);

			var utcNow = DateTime.UtcNow;
			var fileName = $"ReiEditor_crash_{utcNow:yyyyMMdd_HHmmss_fff}.log";
			var filePath = Path.Combine(crashDirectory, fileName);

			var content = new StringBuilder();
			content.AppendLine("REI EDITOR CRASH REPORT");
			content.AppendLine("=======================");
			content.AppendLine($"Timestamp (UTC): {utcNow:yyyy-MM-dd HH:mm:ss.fff}");
			content.AppendLine($"Source: {source}");
			content.AppendLine();
			content.AppendLine(details);

			File.WriteAllText(filePath, content.ToString());
		}
		catch
		{
			// Crash reporting must never throw.
		}
	}
}
