using Avalonia.Controls;
using ReiEditor.ViewModels.Windows.Editor.BuildProject;

namespace ReiEditor.Views.Windows.Editor.BuildProject;

public partial class BuildProjectWindowView : Window
{
    public BuildProjectWindowView()
    {
        InitializeComponent();
        Closing += HandleClosing;
    }

    private void HandleClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not BuildProjectWindowViewModel vm) return;
        if (!vm.IsBuildInProgress) return;

        e.Cancel = true;
        vm.CancelCommand.Execute(null);
    }
}
