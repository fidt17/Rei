using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Logging.EditorConsole;

namespace ReiEditor.ViewModels.Windows.Editor.Console.Commands;

public class ClearEditorConsoleCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;
	
	private readonly IEditorConsoleService _editorConsoleService;

	public ClearEditorConsoleCommand(IEditorConsoleService editorConsoleService)
	{
		_editorConsoleService = editorConsoleService;
		_editorConsoleService.LogsCountChangedEvent += HandleLogsCountChangedEvent;
	}

	public void Dispose()
	{
		_editorConsoleService.LogsCountChangedEvent -= HandleLogsCountChangedEvent;
	}

	private void HandleLogsCountChangedEvent(int count)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}

	public bool CanExecute(object? parameter) => _editorConsoleService.LogsCount > 0;
	
	public void Execute(object? parameter) => _editorConsoleService.ClearConsole();
}