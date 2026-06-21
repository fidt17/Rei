using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property.Custom;

public partial class ColorPropertyView : UserControl
{
    public ColorPropertyView()
    {
        InitializeComponent();
    }

    private void OpenEditorFlyout_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (!Resources.TryGetResource("ColorEditorFlyout", null, out var resource)) return;
        if (resource is not Flyout flyout) return;

        if (flyout.Content is Control content)
        {
            content.DataContext = DataContext;
        }

        flyout.ShowAt(control);
    }
}
