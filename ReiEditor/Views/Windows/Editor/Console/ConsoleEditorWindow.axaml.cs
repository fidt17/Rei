using System;
using Avalonia.Controls;
using ReiEditor.Models.Services.Logging;
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
			vm.NewLogAddedEvent += HandleNewLogAddedEvent;
		}
	}

	private void HandleNewLogAddedEvent(LogMessage message)
	{
		ConsoleScrollViewer.ScrollToEnd();
	}
}