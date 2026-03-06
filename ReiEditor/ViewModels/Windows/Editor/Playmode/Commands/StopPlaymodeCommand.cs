using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

public class StopPlaymodeCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly IEngineRunner _engineRunner;

	public StopPlaymodeCommand(IEngineRunner engineRunner)
	{
		_engineRunner = engineRunner;

		_engineRunner.IsPlaymodeActive.Subscribe(HandlePlaymodeActiveValueChangedEvent);
	}

	public void Dispose()
	{
		_engineRunner.IsPlaymodeActive.Unsubscribe(HandlePlaymodeActiveValueChangedEvent);
	}

	public bool CanExecute(object? parameter) => _engineRunner.IsPlaymodeActive.Value;

	public void Execute(object? parameter) => _engineRunner.StopEngine();

	private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}
}