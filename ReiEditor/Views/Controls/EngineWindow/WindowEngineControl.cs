using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ReiEditor.ViewModels.Windows.Editor.Playmode;

namespace ReiEditor.Views.Controls.EngineWindow;

public class WindowEngineControl : NativeControlHost
{
    private EngineWindowHandle? _handle;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        if (DataContext is EngineWindowProviderViewModel vm)
        {
            vm.ResizeWindow(e.NewSize.Width, e.NewSize.Height);
        }
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        try
        {
            base.CreateNativeControlCore(parent);
            
            if (DataContext is EngineWindowProviderViewModel vm)
            {
                var ptr = vm.WindowHandlePointer;
                _handle = new EngineWindowHandle(vm, ptr, "desc");

                return _handle;
            }

            throw new Exception($"Invalid data context. Expected: {nameof(EngineWindowProviderViewModel)}, Actual: {DataContext?.GetType()}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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