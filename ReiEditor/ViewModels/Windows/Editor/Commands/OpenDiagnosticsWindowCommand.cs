using System;
using System.Windows.Input;
using ReiEditor.Models.EditorApp.Diagnostics;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public sealed class OpenDiagnosticsWindowCommand(IDiagnosticsWindowService service) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => service.Open();
}
