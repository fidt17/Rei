using System;
using Avalonia.Controls;
using Avalonia.Platform;
using ReiEditor.ViewModels.Windows.Editor.Playmode;

namespace ReiEditor.Views.Windows.Editor.Playmode;

public class WindowEngineControl : NativeControlHost
{
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        try
        {
            var ptr = PlaymodePanelViewModel.Instance._engineApi.GetWindowHandle();
            System.Console.WriteLine($"new platform handle. ptr: {ptr}");
            return new PlatformHandle(ptr, "desc");
        }
        catch (Exception e)
        {
            System.Console.WriteLine(e);
        }

        return new PlatformHandle(new IntPtr(0), "empty");
    }
}