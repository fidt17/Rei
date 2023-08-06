using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Editor.ViewModels;
using Editor.Views.Utils;

namespace Editor.Views;

public partial class MainWindow : Window
{
	private MainWindowViewModel? _vm;
	
	public MainWindow()
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
		if (DataContext is MainWindowViewModel vm)
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