using System;
using System.Windows.Input;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

public class StartPlaymodeCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly IPlaymodeService _playmodeService;
	private readonly ILogger<StartPlaymodeCommand> _logger;

	public StartPlaymodeCommand(IPlaymodeService playmodeService, ILogger<StartPlaymodeCommand> logger)
	{
		_playmodeService = playmodeService;
		_logger = logger;
		
		_playmodeService.PlaymodeActiveValueChangedEvent += HandlePlaymodeActiveValueChangedEvent;
	}

	public void Dispose()
	{
		_playmodeService.PlaymodeActiveValueChangedEvent -= HandlePlaymodeActiveValueChangedEvent;
	}

	public bool CanExecute(object? parameter) => _playmodeService.CanStartPlaymode();

	public void Execute(object? parameter)
	{
		try
		{
			_playmodeService.StartPlaymode();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
	}

	private void HandlePlaymodeActiveValueChangedEvent(bool isActive)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		});
	}
}