using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

public class StartPlaymodeCommand : ICommand, IDisposable
{
    public event EventHandler? CanExecuteChanged;

    private readonly IPlaymodeStarter _playmodeStarter;
    private readonly IPlaymodeStartWorkflow _playmodeStartWorkflow;

    public StartPlaymodeCommand(IPlaymodeStarter playmodeStarter, IPlaymodeStartWorkflow playmodeStartWorkflow)
    {
        _playmodeStarter = playmodeStarter;
        _playmodeStartWorkflow = playmodeStartWorkflow;

        _playmodeStarter.CanStart.IsTrue.Subscribe(HandleCanStartPlaymodeValueChangedEvent);
    }

    public void Dispose()
    {
        _playmodeStarter.CanStart.IsTrue.Unsubscribe(HandleCanStartPlaymodeValueChangedEvent);
    }

    public bool CanExecute(object? parameter) => _playmodeStarter.CanStart.IsTrue.Value;

    public void Execute(object? parameter)
    {
        _ = Task.Run(() => _playmodeStartWorkflow.StartAsync());
    }

    private void HandleCanStartPlaymodeValueChangedEvent(bool isActive)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}
