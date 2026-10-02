using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Diagnostics;
using ReiEditor.Views.Windows.Editor.Diagnostics;

namespace ReiEditor.Models.EditorApp.Diagnostics;

public sealed class DiagnosticsWindowService(IFactory<DiagnosticsWindowViewModel> factory, IMainWindowService mainWindow) : IDiagnosticsWindowService, IDisposable
{
    private DiagnosticsWindowView? _window;
    private DiagnosticsWindowViewModel? _viewModel;
    private DispatcherTimer? _timer;

    public void Open()
    {
        if (_window != null) { _window.Activate(); return; }
        var vm = factory.CreateInstance();
        var window = new DiagnosticsWindowView { DataContext = vm };
        _viewModel = vm;
        _window = window;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += HandleTick;
        window.Closed += HandleClosed;
        try
        {
            window.Show(mainWindow.GetMainWindow());
            vm.Refresh();
            _timer.Start();
        }
        catch
        {
            Release();
            window.Close();
            throw;
        }
    }

    public void Dispose()
    {
        _window?.Close();
        Release();
    }

    private void HandleTick(object? sender, EventArgs args)
    {
        if (_window is { IsVisible: true } && _window.WindowState != WindowState.Minimized) _viewModel?.Refresh();
    }

    private void HandleClosed(object? sender, EventArgs args) => Release();

    private void Release()
    {
        if (_timer != null) { _timer.Stop(); _timer.Tick -= HandleTick; _timer = null; }
        if (_window != null) _window.Closed -= HandleClosed;
        _window = null;
        var vm = _viewModel;
        _viewModel = null;
        vm?.Dispose();
    }
}
