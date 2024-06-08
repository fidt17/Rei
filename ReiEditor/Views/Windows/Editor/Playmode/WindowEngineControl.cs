using System;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ReiEditor.ViewModels.Windows.Editor.Rendering;

namespace ReiEditor.Views.Windows.Editor.Playmode;

public class EngineWindowHandle : PlatformHandle, INativeControlHostDestroyableControlHandle
{
    public EngineWindowHandle(IntPtr handle, string? descriptor) : base(handle, descriptor)
    {
    }

    public void Destroy()
    {
        
    }
}

public class WindowEngineControl : NativeControlHost
{
    private EngineWindowHandle? _handle;
    
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        try
        {
            base.CreateNativeControlCore(parent);
            
            if (DataContext is EngineWindowProviderViewModel vm)
            {
                var ptr = vm.WindowPointer;
                _handle = new EngineWindowHandle(ptr, "desc");

                return _handle;
            }

            throw new Exception($"Invalid data context. Expected: {nameof(EngineWindowProviderViewModel)}, Actual: {DataContext?.GetType()}");
        }
        catch (Exception e)
        {
            System.Console.WriteLine(e);
        }

        return new PlatformHandle(new IntPtr(0), "empty");
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (_handle != null)
        {
            DestroyNativeControlCore(_handle);
        }
    }
}