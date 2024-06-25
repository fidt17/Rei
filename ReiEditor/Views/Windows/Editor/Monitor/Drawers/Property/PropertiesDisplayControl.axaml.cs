using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property;

public partial class PropertiesDisplayControl : UserControl
{
    public static readonly StyledProperty<ObservableCollection<BaseViewModel>> ValueProperty = AvaloniaProperty.Register<PropertiesDisplayControl, ObservableCollection<BaseViewModel>>(
        "Value");

    public ObservableCollection<BaseViewModel> Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
    
    public PropertiesDisplayControl()
    {
        InitializeComponent();
    }
}