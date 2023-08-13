using System;
using System.Windows.Input;

namespace ReiEditor.Utils;

public class RelayCommand : ICommand
{
	public event EventHandler? CanExecuteChanged;
	public event Action? ExecutedEvent;

	private readonly Func<bool>? _canExecuteFunction;
	private readonly Action? _executeFunction;

	public RelayCommand(Action? executeFunction = null, Func<bool>? canExecuteFunction = null)
	{
		_canExecuteFunction = canExecuteFunction;
		_executeFunction = executeFunction;
	}

	public bool CanExecute(object? parameter)
	{
		return _canExecuteFunction == null || _canExecuteFunction();
	}

	public void Execute(object? parameter)
	{
		_executeFunction?.Invoke();
		ExecutedEvent?.Invoke();
	}
}