using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace ReiEditor.ViewModels.Windows.Editor.Console.Commands;

public class ClearConsoleCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly ObservableCollection<ConsoleLogMessageViewModel> _logs;

	public ClearConsoleCommand(ObservableCollection<ConsoleLogMessageViewModel> logs)
	{
		_logs = logs;
		_logs.CollectionChanged += HandleCollectionChanged;
	}

	public void Dispose()
	{
		_logs.CollectionChanged -= HandleCollectionChanged;
	}

	private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}

	public bool CanExecute(object? parameter) => _logs.Count > 0;
	public void Execute(object? parameter) => _logs.Clear();
}