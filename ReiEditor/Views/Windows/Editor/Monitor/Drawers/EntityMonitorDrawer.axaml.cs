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
        BehaviourSelectionComboBox.IsVisible = true;
        BehaviourSelectionComboBox.IsDropDownOpen = true;
        BehaviourSelectionComboBox.SelectedIndex = -1;
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
        if (BehaviourSelectionComboBox.SelectedIndex < 0) return;

        if (DataContext is EntityMonitorDrawerViewModel vm)
        {
            vm.AddBehaviour(BehaviourSelectionComboBox.SelectedIndex);
        }
    }
}