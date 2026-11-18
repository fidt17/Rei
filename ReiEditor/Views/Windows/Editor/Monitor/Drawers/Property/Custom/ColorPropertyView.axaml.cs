using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property.Custom;

public partial class ColorPropertyView : UserControl
{
    public ColorPropertyView()
    {
        InitializeComponent();
        if (Resources.TryGetResource("ColorEditorFlyout", null, out var resource) && resource is Flyout { Content: Control content })
        {
            var alpha = content.GetLogicalDescendants().OfType<Slider>().Single(slider => slider.Name == "AlphaSlider");
            alpha.AddHandler(Thumb.DragStartedEvent, ColorDragStarted, handledEventsToo: true);
            alpha.AddHandler(Thumb.DragCompletedEvent, ColorDragCompleted, handledEventsToo: true);
        }
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

    private void ColorDragStarted(object? sender, EventArgs e)
    {
        if (sender is Control { DataContext: ColorPropertyViewModel color }) color.BeginColorDrag();
    }

    private void ColorDragCompleted(object? sender, EventArgs e)
    {
        if (sender is Control { DataContext: ColorPropertyViewModel color }) color.EndColorDrag();
    }
}
