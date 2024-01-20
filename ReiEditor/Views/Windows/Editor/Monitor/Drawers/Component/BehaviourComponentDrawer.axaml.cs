using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Component;

public partial class BehaviourComponentDrawer : UserControl
{
    public BehaviourComponentDrawer()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}