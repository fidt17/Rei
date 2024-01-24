using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers;

public partial class EntityMonitorDrawer : UserControl
{
    public EntityMonitorDrawer()
    {
        InitializeComponent();
    }

    private void AddBehaviourButtonClicked(object? sender, RoutedEventArgs e)
    {
        ShowBehaviourComboBox();
    }

    private void ShowAddBehaviourButton()
    {
        AddBehaviourButton.IsVisible = true;
        BehaviourSelectionComboBox.IsVisible = false;
        BehaviourSelectionComboBox.IsDropDownOpen = false;
    }

    private void ShowBehaviourComboBox()
    {
        AddBehaviourButton.IsVisible = false;
        BehaviourSelectionComboBox.SelectedIndex = -1;
        BehaviourSelectionComboBox.IsVisible = true;
        BehaviourSelectionComboBox.IsDropDownOpen = true;
    }

    private void BehaviourComboboxLostFocus(object? sender, RoutedEventArgs e)
    {
        ShowAddBehaviourButton();
    }

    private void BehaviourComboboxClosed(object? sender, EventArgs e)
    {
        ShowAddBehaviourButton();
    }

    private void BehaviourComboboxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!BehaviourSelectionComboBox.IsVisible) return;
        if (e.AddedItems.Count != 1) return;

        BehaviourSelectionData? item = (BehaviourSelectionData?) e.AddedItems[0];
        if (item == null) return;
        if (DataContext is EntityMonitorDrawerViewModel vm)
        {
            vm.AddBehaviour(item);
        }
    }
}