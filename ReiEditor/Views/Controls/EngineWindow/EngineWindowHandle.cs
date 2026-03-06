using System;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using ReiEditor.ViewModels.Windows.Editor.Playmode;

namespace ReiEditor.Views.Controls.EngineWindow;

public class EngineWindowHandle : PlatformHandle, INativeControlHostDestroyableControlHandle
{
    public readonly EngineWindowProviderViewModel ViewModel;
    
    public EngineWindowHandle(EngineWindowProviderViewModel vm, IntPtr handle, string? descriptor) : base(handle, descriptor)
    {
        ViewModel = vm;
    }

    public void Destroy() { }
}