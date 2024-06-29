using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Build;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class BuildProjectCommand : ICommand, IDisposable
{
    private readonly IBuildStarter _buildStarter;
    public event EventHandler? CanExecuteChanged;

    public BuildProjectCommand(IBuildStarter buildStarter)
    {
        _buildStarter = buildStarter;
        _buildStarter.CanStartBuild.IsTrue.Subscribe(HandleCanStartBuildChangedEvent);
    }

    public void Dispose()
    {
        _buildStarter.CanStartBuild.IsTrue.Unsubscribe(HandleCanStartBuildChangedEvent);
    }

    public bool CanExecute(object? parameter) => _buildStarter.CanStartBuild.IsTrue.Value;

    public void Execute(object? parameter)
    {
        Task.Run(() => _buildStarter.BuildProject(BuildConfigurationEnum.EditorDebug));
    }
	
    private void HandleCanStartBuildChangedEvent(bool value)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}