using System;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Windows.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class EngineWindowProviderViewModel
{
    public readonly IntPtr WindowHandlePointer;

    private readonly IntPtr _windowPointer;
    private readonly IEngineApi _engineApi;
    private readonly IEngineWindowController _engineWindowController;

    public EngineWindowProviderViewModel(IntPtr windowPointer, IEngineApi engineApi, IEngineWindowController engineWindowController)
    {
        _windowPointer = windowPointer;
        WindowHandlePointer = engineApi.GetWindowHandle(windowPointer);

        _engineApi = engineApi;
        _engineWindowController = engineWindowController;
    }

    public void ResizeWindow(double width, double height)
    {
        if (!_engineApi.IsEngineRunning) return;
        if (width <= 0 || height <= 0) return;

        try
        {
            var intWidth = (int)width;
            var intHeight = (int)height;
            _engineWindowController.SetViewportSize(intWidth, intHeight);
            _engineApi.ResizeWindow(_windowPointer, intWidth, intHeight);
        }
        catch
        {
            // ignored
        }
    }
}
