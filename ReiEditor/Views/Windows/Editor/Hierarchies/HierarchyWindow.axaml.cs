using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyWindow : UserControl
{
    private HierarchyWindowViewModel? _vm;

    public HierarchyWindow()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChangedEvent;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        UnsubscribeFromVm();
    }

    private void HierarchyContentBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;

        if (e.GetCurrentPoint(control).Properties.IsRightButtonPressed)
        {
            FlyoutBase.ShowAttachedFlyout(control);
            e.Handled = true;
            
            return;
        }

        if (DataContext is not HierarchyWindowViewModel vm) return;
        var command = vm.ResetSelectionCommand;
        if (command == null) return;

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromVm();
        if (DataContext is not HierarchyWindowViewModel vm) return;

        _vm = vm;
        _vm.RootContextMenu.AnyCommandExecutedEvent += HandleAnyRootContextMenuCommandExecuted;
    }

    private void UnsubscribeFromVm()
    {
        if (_vm == null) return;

        _vm.RootContextMenu.AnyCommandExecutedEvent -= HandleAnyRootContextMenuCommandExecuted;
        _vm = null;
    }

    private void HandleAnyRootContextMenuCommandExecuted()
    {
        var flyout = FlyoutBase.GetAttachedFlyout(RootBorder);
        flyout?.Hide();
    }
}
