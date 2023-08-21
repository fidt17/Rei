using System;
using Avalonia.Controls;
using ReiEditor.ViewModels.Windows.Editor.Console;

namespace ReiEditor.Views.Windows.Editor.Console;

public partial class ConsoleEditorWindow : UserControl
{
	public ConsoleEditorWindow()
	{
		InitializeComponent();
		DataContextChanged += HandleDataContextChanged;
	}

	private void HandleDataContextChanged(object? sender, EventArgs e)
	{
		if (DataContext is ConsoleEditorWindowViewModel vm)
		{
			vm.LogCollectionUpdated += HandleLogCollectionUpdated;
		}
	}

	private void HandleLogCollectionUpdated()
	{
		if (Math.Abs(ConsoleScrollViewer.ScrollBarMaximum.Y - ConsoleScrollViewer.Offset.Y) > 10) return;
		ConsoleScrollViewer.ScrollToEnd();
	}
}