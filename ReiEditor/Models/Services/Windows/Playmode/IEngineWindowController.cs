using System;

namespace ReiEditor.Models.Services.Windows.Playmode;

public interface IEngineWindowController
{
    Utils.Common.IObservable<IntPtr?> WindowPointer { get; }
    Utils.Common.IObservable<(int Width, int Height)?> ViewportSize { get; }

    void SetupWindow();
    void DestroyWindow();
    void SetViewportSize(int width, int height);
}
