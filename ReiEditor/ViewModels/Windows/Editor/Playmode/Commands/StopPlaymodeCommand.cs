using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

public class StopPlaymodeCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly IPlaymodeService _playmodeService;

	public StopPlaymodeCommand(IPlaymodeService playmodeService)
	{
		_playmodeService = playmodeService;
		
		_playmodeService.PlaymodeActiveValueChangedEvent += HandlePlaymodeActiveValueChangedEvent;
	}

	public void Dispose()
	{
		_playmodeService.PlaymodeActiveValueChangedEvent -= HandlePlaymodeActiveValueChangedEvent;
	}

	public bool CanExecute(object? parameter) => _playmodeService.CanStopPlaymode();

	public void Execute(object? parameter)
	{
		Task.Run(_playmodeService.StopPlaymode);
	}

	private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}
}