using System.Threading.Tasks;
using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Editor.Models.Services.Logging;
using Editor.Startup;
using Editor.Views;

namespace Editor;

public class App : Application
{
	private EditorScope _editorScope = null!;
	private ILogger<App> _logger = null!;
	
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		base.OnFrameworkInitializationCompleted();
		
		_editorScope = new EditorScope();
		var container = _editorScope.Configure();

		_logger = container.Resolve<ILogger<App>>();

		SetupMainWindow(container.Resolve<MainWindow>());
		container.Resolve<EditorEntryPoint>().Start();
	}
	
	private void SetupMainWindow(Window window)
	{
		window.Closed += (_, _) =>
		{
			_logger.LogWarning("Main window closed");
			Task.Run(() => ShutdownAsync(1));
		};
		
		if (Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = window;
		}
		
		window.Show();
	}

	private async Task ShutdownAsync(int exitCode)
	{
		_logger.Log($"Application shutdown... Exit code {exitCode}");
		
		if (_editorScope != null)
		{
			await _editorScope.DisposeAsync();
		}
		
		if (Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}
}