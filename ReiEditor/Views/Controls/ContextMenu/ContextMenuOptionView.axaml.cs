using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

        var nestedFlyout = GetNestedFlyout();
        if (nestedFlyout != null)
        {
            nestedFlyout.Opened += (_, _) =>
            {
                _nestedFlyoutOpenedAtUtc = DateTime.UtcNow;
                SetSubmenuOpenVisualState(true);
            };
            nestedFlyout.Closed += (_, _) =>
            {
                _nestedFlyoutOpenedAtUtc = DateTime.MinValue;
                SetSubmenuOpenVisualState(false);
            };
        }
    }

    private void OptionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContextMenuOption option) return;
        if (!option.HasNestedMenu) return;
        if (sender is not Control control) return;
        if (GetNestedFlyout() is not Flyout nestedFlyout) return;

        _shouldCloseNestedMenu = false;
        if (nestedFlyout.IsOpen) return;

        FlyoutBase.ShowAttachedFlyout(control);
    }

    private void HoverRegion_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _shouldCloseNestedMenu = false;
        var nestedFlyout = GetNestedFlyout();
        if (nestedFlyout == null || !nestedFlyout.IsOpen) return;
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
        
        var nestedFlyout = GetNestedFlyout();
        if (nestedFlyout == null || !nestedFlyout.IsOpen) return;

        var elapsedSinceOpen = DateTime.UtcNow - _nestedFlyoutOpenedAtUtc;
        if (elapsedSinceOpen < InitialCloseSuppression)
        {
            ScheduleCloseCheck(InitialCloseSuppression - elapsedSinceOpen);
            return;
        }

        nestedFlyout.Hide();
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

    private Flyout? GetNestedFlyout()
    {
        return FlyoutBase.GetAttachedFlyout(OptionButton) as Flyout;
    }
}
