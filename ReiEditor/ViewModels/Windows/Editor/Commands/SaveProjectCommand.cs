using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class SaveProjectCommand : ICommand, IDisposable
{
    public event EventHandler? CanExecuteChanged;
    
    private readonly IAssetsService _assetsService;
    private readonly IPlaymodeService _playmodeService;
    private readonly IBuildService _buildService;

    public SaveProjectCommand(IAssetsService assetsService, IPlaymodeService playmodeService, IBuildService buildService)
    {
        _assetsService = assetsService;
        _playmodeService = playmodeService;
        _buildService = buildService;
        
        _playmodeService.IsPlaymodeActive.Subscribe(HandleIsPlaymodeActiveValueChanged, invoke: false);
        _buildService.BuildInProgress.Subscribe(HandleBuildInProgressChanged, invoke: false);
    }

    public void Dispose()
    {
        _playmodeService.IsPlaymodeActive.Unsubscribe(HandleIsPlaymodeActiveValueChanged);
        _buildService.BuildInProgress.Unsubscribe(HandleBuildInProgressChanged);
    }

    public bool CanExecute(object? parameter)
    {
        if (_assetsService.SaveInProcess.Value) return false;
        if (_playmodeService.IsPlaymodeActive.Value) return false;
        if (_buildService.BuildInProgress.Value) return false;

        return true;
    }

    public void Execute(object? parameter)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _assetsService.SaveProject();
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
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