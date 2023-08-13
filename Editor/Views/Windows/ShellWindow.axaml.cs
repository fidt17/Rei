using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Editor.ViewModels;
using Editor.Views.Utils;

namespace Editor.Views;

public partial class ShellWindow : Window
{
	private ShellWindowViewModel? _vm;
	
	public ShellWindow()
	{
		DataContextChanged += HandleDataContextChange;
		
		InitializeComponent();
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		
		if (_vm != null)
		{
			_vm.ActiveTab.ChangedEvent -= HandleWindowTabChangedEvent;
		}
	}

	private void HandleDataContextChange(object? sender, EventArgs e)
	{
		if (DataContext is ShellWindowViewModel vm)
		{
			_vm = vm;
			_vm.ActiveTab.ChangedEvent += HandleWindowTabChangedEvent;
		}
	}

	private void HandleWindowTabChangedEvent()
	{
		Dispatcher.UIThread.Invoke(this.CenterWindow, DispatcherPriority.ContextIdle);
	}
}