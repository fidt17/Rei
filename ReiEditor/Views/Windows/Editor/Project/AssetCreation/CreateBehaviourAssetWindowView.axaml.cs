using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows.Editor.Project.AssetCreation;

public partial class CreateBehaviourAssetWindowView : Window
{
    public CreateBehaviourAssetWindowView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
