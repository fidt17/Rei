using System;
using Avalonia.Controls;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.AssetCreation.Common;

public class SingleDialogWindowCoordinator
{
    private Window? _window;

    private readonly IMainWindowService _mainWindowService;
    private readonly ILogger _logger;

    public SingleDialogWindowCoordinator(IMainWindowService mainWindowService, ILogger logger)
    {
        _mainWindowService = mainWindowService;
        _logger = logger;
    }

    public void Open<TViewModel>(
        Func<TViewModel> createViewModel,
        Func<TViewModel, Window> createWindow)
        where TViewModel : class, IDisposable
    {
        if (_window != null)
        {
            _logger.LogWarning("Material creation window is already opened.");
            return;
        }

        var viewModel = createViewModel();
        var window = createWindow(viewModel);

        void HandleWindowClosed(object? _, EventArgs __)
        {
            window.Closed -= HandleWindowClosed;
            viewModel.Dispose();
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        }

        window.Closed += HandleWindowClosed;
        _window = window;

        try
        {
            _mainWindowService.ShowDialog(window);
        }
        catch (Exception e)
        {
            window.Closed -= HandleWindowClosed;
            viewModel.Dispose();
            _window = null;
            _logger.LogException(e);
        }
    }

    public void Close()
    {
        if (_window == null)
        {
            _logger.LogWarning("Cannot close creation window because it is not opened.");
            return;
        }

        _window.Close();
    }
}