using System;

namespace ReiEditor.ViewModels.Windows.Editor.Rendering;

public class EngineWindowProviderViewModel
{
    public readonly IntPtr WindowPointer;

    public EngineWindowProviderViewModel(IntPtr windowPointer)
    {
        WindowPointer = windowPointer;
    }
}