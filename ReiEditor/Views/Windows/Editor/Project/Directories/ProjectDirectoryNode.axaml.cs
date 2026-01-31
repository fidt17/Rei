using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.ViewModels.Windows.Editor.Project.Directories;

namespace ReiEditor.Views.Windows.Editor.Project.Directories;

public partial class ProjectDirectoryNode : UserControl
{
    private ProjectDirectoryNodeViewModel? _vm;

    public ProjectDirectoryNode()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChangedEvent;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _vm = null;
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        _vm = DataContext as ProjectDirectoryNodeViewModel;
    }

    private void RootBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null) return;
        _vm.SelectCommand.Execute(null);
    }
}
