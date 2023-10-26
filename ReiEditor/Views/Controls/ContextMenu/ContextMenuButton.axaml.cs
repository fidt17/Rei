using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.Views.Controls.ContextMenu;

public partial class ContextMenuButton : UserControl
{
	public static readonly StyledProperty<ContextMenuViewModel> ContextMenuDataProperty = AvaloniaProperty.Register<ContextMenuButton, ContextMenuViewModel>("ContextMenuData");

	public ContextMenuViewModel ContextMenuData
	{
		get => GetValue(ContextMenuDataProperty);
		set => SetValue(ContextMenuDataProperty, value);
	}
	
	public ContextMenuButton()
	{
		InitializeComponent();
		ContextMenuDataProperty.Changed.AddClassHandler<ContextMenuButton>((x, e) => x.ContextMenuDataChanged((ContextMenuViewModel?)e.NewValue));
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		base.OnUnloaded(e);

		if (ContextMenuData != null)
		{
			ContextMenuData.AnyCommandExecutedEvent -= HandleAnyCommandExecutedEvent;
		}
	}

	private void ContextMenuDataChanged(ContextMenuViewModel? vm)
	{
		if (vm == null) return;
		vm.AnyCommandExecutedEvent += HandleAnyCommandExecutedEvent;
	}

	private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
		{
			FlyoutBase.ShowAttachedFlyout((sender as Control)!);
		}
	}

	private void HandleAnyCommandExecutedEvent()
	{
		var f = FlyoutBase.GetAttachedFlyout(rootBorder);
		f?.Hide();
	}
}