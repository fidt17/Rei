using System;
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
		try
		{
			var appBuilder = BuildAvaloniaApp();
			appBuilder.StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
		}
		catch (Exception e)
		{
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
}