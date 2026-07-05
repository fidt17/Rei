using Avalonia.Controls;
using Avalonia.Input;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property.Custom;

public partial class RectTransformPropertyView : UserControl
{
    public RectTransformPropertyView()
    {
        InitializeComponent();
    }

    private void CommitFocusedTextBox_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Root.Focus();
    }
}
