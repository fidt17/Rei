using System;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ReiEditor.Views.Utils;

public static class ControlExtensions
{
    public static Window GetWindow(this UserControl control)
    {
        return control.GetVisualRoot() as Window ?? throw new Exception($"Could not find root window on {control}");
    }
}