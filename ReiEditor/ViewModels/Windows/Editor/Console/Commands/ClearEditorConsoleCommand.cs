using System;
using System.Windows.Input;
using ReiEditor.Models.EditorApp.Console;

namespace ReiEditor.ViewModels.Windows.Editor.Console.Commands;

public class ClearEditorConsoleCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;

    private readonly IEditorConsoleService _editorConsoleService;

    public ClearEditorConsoleCommand(IEditorConsoleService editorConsoleService)
    {
        _editorConsoleService = editorConsoleService;
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public bool CanExecute(object? parameter) => _editorConsoleService.LogsCount.Value > 0;

    public void Execute(object? parameter) => _editorConsoleService.ClearConsole();
}
