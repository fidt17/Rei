using System;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Editor.Startup.Scopes;

namespace Editor;

public class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		base.OnFrameworkInitializationCompleted();
		StartApplication();
	}

	private static void StartApplication()
	{
		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			try
			{
				var applicationScope = new ApplicationScope();
				await applicationScope.StartAsync();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}
		});
	}
}