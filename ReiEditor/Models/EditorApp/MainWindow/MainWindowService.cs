using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReiEditor.Models.EditorApp.Shutdown;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.MainWindow;

public class MainWindowService : IMainWindowService
{
	private Window? _mainWindow;
	
	private readonly ILogger<MainWindowService> _logger;
	private readonly IApplicationShutdownService _shutdownService;

	public MainWindowService(IApplicationShutdownService shutdownService, ILogger<MainWindowService> logger)
	{
		_shutdownService = shutdownService;
		_logger = logger;
	}

	public Window GetMainWindow() => _mainWindow ?? throw new NullReferenceException(nameof(_mainWindow));

	public void ShowMainWindow(Window window)
	{
		if (_mainWindow != null)
		{
			_mainWindow.Closed -= HandleWindowClosedEvent;
			_mainWindow.Close();
		}
		
		_mainWindow = window;
		
		if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = _mainWindow;
		}
		
		_mainWindow.Show();
		_mainWindow.Closed += HandleWindowClosedEvent;
	}

	public void ShowDialog(Window window)
	{
		if (_mainWindow == null)
		{
			_logger.LogError("Cannot open dialog window because main window is missing");
			return;
		}
		
		window.ShowDialog(_mainWindow);
	}

	private void HandleWindowClosedEvent(object? sender, EventArgs e)
	{
		_logger.LogWarning("Main window closed");
		_shutdownService.Shutdown(1);
	}
}