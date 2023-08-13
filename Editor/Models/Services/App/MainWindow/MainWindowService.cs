using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Editor.Models.Services.App.Shutdown;
using Editor.Models.Services.Logging;

namespace Editor.Models.Services.App.MainWindow;

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
		_mainWindow = window;
		
		if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = _mainWindow;
		}
		
		_mainWindow.Show();
		_mainWindow.Closed += HandleWindowClosedEvent;
	}

	private void HandleWindowClosedEvent(object? sender, EventArgs e)
	{
		if (!Equals(_mainWindow, sender)) return;
		
		_logger.LogWarning("Main window closed");
		_shutdownService.Shutdown(1);
	}
}