using System;
using System.Windows.Input;
using ReiEditor.Models.EditorApp.ProjectBuildWindow;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class OpenBuildProjectWindowCommand : ICommand, IDisposable
{
    public event EventHandler? CanExecuteChanged;

    private readonly IProjectBuildWindowService _projectBuildWindowService;

    public OpenBuildProjectWindowCommand(IProjectBuildWindowService projectBuildWindowService)
    {
        _projectBuildWindowService = projectBuildWindowService;
        _projectBuildWindowService.IsOpened.Subscribe(HandleIsOpenedChanged);
    }

    public void Dispose()
    {
        _projectBuildWindowService.IsOpened.Unsubscribe(HandleIsOpenedChanged);
    }

    public bool CanExecute(object? parameter) => !_projectBuildWindowService.IsOpened.Value;

    public void Execute(object? parameter)
    {
        _projectBuildWindowService.OpenWindow();
    }

    private void HandleIsOpenedChanged(bool _)
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
