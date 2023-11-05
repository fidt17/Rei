using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyNode : UserControl
{
    private HierarchyNodeViewModel? _vm;
    
    public HierarchyNode()
    {
        InitializeComponent();
        
        DataContextChanged += HandleDataContextChangedEvent;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        UnsubscribeFromVm();
    }

    private void UnsubscribeFromVm()
    {
        if (_vm == null) return;
        _vm.StartRenameCommand.ExecutedEvent -= EnableNameTextBox;
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromVm();
        if (DataContext is not HierarchyNodeViewModel vm) return;
        _vm = vm;

        _vm.StartRenameCommand.ExecutedEvent += EnableNameTextBox;
    }

    private void EnableNameTextBox()
    {
        NameTextBox.IsVisible = true;
        NameTextBox.Focus(NavigationMethod.Pointer);
        NameTextBox.SelectAll();
    }

    private void RootBorder_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm == null) return;
        if (!_vm.Selected.Value) return;
        if (e.Key != Key.Delete) return;
		
        _vm.DeleteCommand.Execute(null);
    }

    private void InputElement_OnTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null) return;
        if (!_vm.Selected.Value) return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            const int DELAY = 300;
            await Task.Delay(DELAY);
            
            if (!_vm.Selected.Value) return;
            _vm.StartRenameCommand.Execute(null);
        });
    }

    private void NameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RootBorder.Focus();
        }
        else if (e.Key == Key.Enter)
        {
            var vm = DataContext as HierarchyNodeViewModel;
            vm?.ConfirmRenameCommand.Execute(NameTextBox.Text);
            RootBorder.Focus();
        }
    }
}