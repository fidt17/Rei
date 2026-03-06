using Avalonia;
using Avalonia.Controls;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property;

public partial class EnumPropertyView : UserControl
{
    public static readonly StyledProperty<int> ComboBoxWidthProperty = AvaloniaProperty.Register<EnumPropertyView, int>("ComboBoxWidth", 300);

    public int ComboBoxWidth
    {
        get => GetValue(ComboBoxWidthProperty);
        set => SetValue(ComboBoxWidthProperty, value);
    }
    
    public EnumPropertyView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        SetValue(ComboBoxWidthProperty, (int) (RootBorder.Bounds.Width - PropertyNameTextBlock.Bounds.Width - 5));
    }
}