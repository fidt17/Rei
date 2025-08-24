using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

public class StartPlaymodeCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly IPlaymodeStarter _playmodeStarter;

	public StartPlaymodeCommand(IPlaymodeStarter playmodeStarter)
	{
		_playmodeStarter = playmodeStarter;

		_playmodeStarter.CanStart.IsTrue.Subscribe(HandleCanStartPlaymodeValueChangedEvent);
	}

	public void Dispose()
	{
		_playmodeStarter.CanStart.IsTrue.Unsubscribe(HandleCanStartPlaymodeValueChangedEvent);
	}

	public bool CanExecute(object? parameter) => _playmodeStarter.CanStart.IsTrue.Value;

	public void Execute(object? parameter) => _playmodeStarter.Start();

	private void HandleCanStartPlaymodeValueChangedEvent(bool isActive)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}
}