using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

public class StartPlaymodeCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly IPlaymodeStarter _playmodeStarter;

	public StartPlaymodeCommand(IPlaymodeStarter playmodeStarter)
	{
		_playmodeStarter = playmodeStarter;

		_playmodeStarter.CanStartPlaymode.IsTrue.Subscribe(HandleCanStartPlaymodeValueChangedEvent);
	}

	public void Dispose()
	{
		_playmodeStarter.CanStartPlaymode.IsTrue.Unsubscribe(HandleCanStartPlaymodeValueChangedEvent);
	}

	public bool CanExecute(object? parameter) => _playmodeStarter.CanStartPlaymode.IsTrue.Value;

	public void Execute(object? parameter) => _playmodeStarter.StartPlaymode();

	private void HandleCanStartPlaymodeValueChangedEvent(bool isActive)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}
}