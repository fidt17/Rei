using System;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode;

public class EngineWindowProviderViewModel
{
    public readonly IntPtr WindowHandlePointer;

    private readonly IntPtr _windowPointer;
    private readonly IEngineApi _engineApi;

    public EngineWindowProviderViewModel(IntPtr windowPointer, IEngineApi engineApi)
    {
        _windowPointer = windowPointer;
        WindowHandlePointer = engineApi.GetWindowHandle(windowPointer);

        _engineApi = engineApi;
    }

    public void ResizeWindow(double width, double height)
    {
        if (!_engineApi.IsEngineRunning) return;
        if (width <= 0 || height <= 0) return;

        try
        {
            _engineApi.ResizeWindow(_windowPointer, (int)width, (int)height);
        }
        catch
        {
        }
    }
}
