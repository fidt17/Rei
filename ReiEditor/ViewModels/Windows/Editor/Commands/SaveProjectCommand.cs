using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class SaveProjectCommand : ICommand, IDisposable
{
    public event EventHandler? CanExecuteChanged;
    
    private readonly IAssetsService _assetsService;
    private readonly IEngineRunner _engineRunner;
    private readonly IBuildService _buildService;
    private readonly ISceneStateSynchronizer _sceneStateSynchronizer;

    public SaveProjectCommand(IAssetsService assetsService, IBuildService buildService, IEngineRunner engineRunner, ISceneStateSynchronizer sceneStateSynchronizer)
    {
        _assetsService = assetsService;
        _buildService = buildService;
        _engineRunner = engineRunner;
        _sceneStateSynchronizer = sceneStateSynchronizer;

        _engineRunner.IsPlaymodeActive.Subscribe(HandleIsPlaymodeActiveValueChanged, invoke: false);
        _buildService.BuildInProgress.Subscribe(HandleBuildInProgressChanged, invoke: false);
    }

    public void Dispose()
    {
        _engineRunner.IsPlaymodeActive.Unsubscribe(HandleIsPlaymodeActiveValueChanged);
        _buildService.BuildInProgress.Unsubscribe(HandleBuildInProgressChanged);
    }

    public bool CanExecute(object? parameter)
    {
        if (_assetsService.SaveInProcess.Value) return false;
        if (_engineRunner.IsPlaymodeActive.Value) return false;
        if (_buildService.BuildInProgress.Value) return false;

        return true;
    }

    public void Execute(object? parameter)
    {
        Dispatcher.UIThread.InvokeAsync(SaveProject);
    }

    public async Task SaveProject()
    {
        _sceneStateSynchronizer.SynchronizeStateWithEngine();
        await _assetsService.SaveProject();
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleBuildInProgressChanged(bool _)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void HandleIsPlaymodeActiveValueChanged(bool _)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}