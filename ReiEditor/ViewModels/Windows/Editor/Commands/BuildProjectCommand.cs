using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class BuildProjectCommand : ICommand, IDisposable
{
    private readonly IBuildStarter _buildStarter;
    private readonly IEditorModeStarter _editorModeStarter;
    private readonly SaveProjectCommand _saveProjectCommand;
    
    public event EventHandler? CanExecuteChanged;

    public BuildProjectCommand(IBuildStarter buildStarter, IEditorModeStarter editorModeStarter, SaveProjectCommand saveProjectCommand)
    {
        _buildStarter = buildStarter;
        _editorModeStarter = editorModeStarter;
        _saveProjectCommand = saveProjectCommand;
        _buildStarter.CanStartBuild.IsTrue.Subscribe(HandleCanStartBuildChangedEvent);
    }

    public void Dispose()
    {
        _buildStarter.CanStartBuild.IsTrue.Unsubscribe(HandleCanStartBuildChangedEvent);
    }

    public bool CanExecute(object? parameter) => _buildStarter.CanStartBuild.IsTrue.Value;

    public void Execute(object? parameter)
    {
        Task.Run(async () =>
        {
            await _saveProjectCommand.SaveProject();
            await _buildStarter.BuildProject(BuildConfigurationEnum.EditorDebug);
            _editorModeStarter.Start();
        });
    }
	
    private void HandleCanStartBuildChangedEvent(bool value)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}