using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using ReiEditor.Models.Services.Logging;

namespace ReiEditor.ViewModels.Windows.Editor.Console.Commands;

public class ClearConsoleCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly ObservableCollection<LogMessage> _logs;
	private readonly ObservableCollection<ConsoleLogMessageViewModel> _filteredLogs;

	public ClearConsoleCommand(ObservableCollection<LogMessage> logs, ObservableCollection<ConsoleLogMessageViewModel> filteredLogs)
	{
		_logs = logs;
		_filteredLogs = filteredLogs;
		
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
	public void Execute(object? parameter)
	{
		_logs.Clear();
		
		foreach (var vm in _filteredLogs)
		{
			vm.Dispose();
		}
		_filteredLogs.Clear();
	}
}