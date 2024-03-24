using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property;

public partial class BooleanPropertyView : UserControl
{
    public BooleanPropertyView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}