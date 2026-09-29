using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ReiEditor.ViewModels.Controls;
using System;
using Avalonia.Interactivity;

namespace ReiEditor.Views.Controls.ContextMenu;

public partial class ContextMenuOptionView : UserControl
{
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan InitialCloseSuppression = TimeSpan.FromMilliseconds(100);

    private bool _shouldCloseNestedMenu;
    
    private DateTime _nestedFlyoutOpenedAtUtc = DateTime.MinValue;

    public ContextMenuOptionView()
    {
        InitializeComponent();

        NestedMenuPopup.PlacementTarget = OptionButton;
        NestedMenuPopup.Opened += (_, _) =>
        {
            _nestedFlyoutOpenedAtUtc = DateTime.UtcNow;
            SetSubmenuOpenVisualState(true);
        };
        NestedMenuPopup.Closed += (_, _) =>
        {
            _nestedFlyoutOpenedAtUtc = DateTime.MinValue;
            SetSubmenuOpenVisualState(false);
        };
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        NestedMenuPopup.IsOpen = false;
        base.OnUnloaded(e);
    }

    private void OptionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContextMenuOption option) return;
        if (!option.HasNestedMenu) return;
        _shouldCloseNestedMenu = false;
        if (NestedMenuPopup.IsOpen) return;

        NestedMenuPopup.IsOpen = true;
    }

    private void HoverRegion_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _shouldCloseNestedMenu = false;
        if (!NestedMenuPopup.IsOpen) return;
        SetSubmenuOpenVisualState(true);
    }

    private void HoverRegion_OnPointerExited(object? sender, PointerEventArgs e)
    {
        ScheduleCloseCheck();
    }

    private void ScheduleCloseCheck(TimeSpan? delay = null)
    {
        _shouldCloseNestedMenu = true;
        DispatcherTimer.RunOnce(TryCloseNestedMenu, delay ?? CloseDelay);
    }

    private void TryCloseNestedMenu()
    {
        if (!_shouldCloseNestedMenu) return;
        if (OptionButton.IsPointerOver) return;
        if (NestedMenuView.IsPointerOver) return;
        
        if (!NestedMenuPopup.IsOpen) return;

        var elapsedSinceOpen = DateTime.UtcNow - _nestedFlyoutOpenedAtUtc;
        if (elapsedSinceOpen < InitialCloseSuppression)
        {
            ScheduleCloseCheck(InitialCloseSuppression - elapsedSinceOpen);
            return;
        }

        NestedMenuPopup.IsOpen = false;
    }

    private void SetSubmenuOpenVisualState(bool isOpen)
    {
        if (isOpen)
        {
            OptionButton.Classes.Add("SubmenuOpen");
            return;
        }

        OptionButton.Classes.Remove("SubmenuOpen");
    }
}
