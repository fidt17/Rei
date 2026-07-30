using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.TransformationControls;

namespace ReiEditor.Models.EditorApp.Hotkeys;

public sealed class EditorTransformationHotkeyService : IDisposable
{
    private readonly IMainWindowService _mainWindowService;
    private readonly ITransformationControlsService _transformationControlsService;

    private Window? _window;

    public EditorTransformationHotkeyService(IMainWindowService mainWindowService, ITransformationControlsService transformationControlsService)
    {
        _mainWindowService = mainWindowService;
        _transformationControlsService = transformationControlsService;

        _mainWindowService.ActivatedEvent += HandleMainWindowActivated;
        TryAttachToMainWindow();
    }

    public void Dispose()
    {
        _mainWindowService.ActivatedEvent -= HandleMainWindowActivated;
        DetachFromWindow();
    }

    private void HandleMainWindowActivated()
    {
        TryAttachToMainWindow();
    }

    private void TryAttachToMainWindow()
    {
        Window window;
        try
        {
            window = _mainWindowService.GetMainWindow();
        }
        catch
        {
            return;
        }

        if (_window == window) return;

        DetachFromWindow();
        _window = window;
        _window.AddHandler(InputElement.KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
    }

    private void DetachFromWindow()
    {
        if (_window == null) return;

        _window.RemoveHandler(InputElement.KeyDownEvent, HandleKeyDown);
        _window = null;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        if (e.KeyModifiers != KeyModifiers.None) return;

        switch (e.Key)
        {
            case Key.W:
                _transformationControlsService.SetMode(TransformationMode.Movement);
                e.Handled = true;
                break;
            case Key.E:
                _transformationControlsService.SetMode(TransformationMode.Scale);
                e.Handled = true;
                break;
            case Key.R:
                _transformationControlsService.SetMode(TransformationMode.Rotation);
                e.Handled = true;
                break;
            case Key.T:
                _transformationControlsService.SetMode(TransformationMode.RectTransform);
                e.Handled = true;
                break;
        }
    }
}
