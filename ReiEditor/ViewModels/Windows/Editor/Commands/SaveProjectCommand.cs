using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Commands;

public class SaveProjectCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;
    
    private readonly IAssetsService _assetsService;

    public SaveProjectCommand(IAssetsService assetsService)
    {
        _assetsService = assetsService;
    }

    public bool CanExecute(object? parameter) => !_assetsService.SaveInProcess.Value;

    public void Execute(object? parameter)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _assetsService.SaveProject();
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}