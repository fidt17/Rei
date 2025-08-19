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

    // ReSharper disable once UnusedParameter.Local
    private void AddBehaviourButtonClicked(object? sender, RoutedEventArgs _)
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

    // ReSharper disable once UnusedParameter.Local
    private void BehaviourComboboxLostFocus(object? sender, RoutedEventArgs _)
    {
        ShowAddBehaviourButton();
    }

    // ReSharper disable once UnusedParameter.Local
    private void BehaviourComboboxClosed(object? sender, EventArgs _)
    {
        ShowAddBehaviourButton();
    }

    private void BehaviourComboboxSelectionChanged(object? _, SelectionChangedEventArgs e)
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