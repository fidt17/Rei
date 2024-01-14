using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Console;

namespace ReiEditor.ViewModels.Windows.Editor.Console.Commands;

public class ClearEditorConsoleCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;
	
	private readonly IEditorConsoleService _editorConsoleService;

	public ClearEditorConsoleCommand(IEditorConsoleService editorConsoleService)
	{
		_editorConsoleService = editorConsoleService;
		_editorConsoleService.LogsCount.Subscribe(HandleLogsCountChangedEvent);
	}

	public void Dispose()
	{
		_editorConsoleService.LogsCount.Unsubscribe(HandleLogsCountChangedEvent);
	}

	private void HandleLogsCountChangedEvent(int count)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}

	public bool CanExecute(object? parameter) => _editorConsoleService.LogsCount.Value > 0;
	
	public void Execute(object? parameter) => _editorConsoleService.ClearConsole();
}