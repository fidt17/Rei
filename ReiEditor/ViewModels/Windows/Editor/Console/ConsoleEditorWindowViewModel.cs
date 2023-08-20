using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReiEditor.Models.Services.Logging;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleEditorWindowViewModel : BaseViewModel
{
	public event Action<LogMessage>? NewLogAddedEvent;
	
	public ObservableCollection<ConsoleLogMessageViewModel> Logs { get; } = new();

	private readonly IEditorConsoleService _consoleService;

#pragma warning disable CS8618
	public ConsoleEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ConsoleEditorWindowViewModel(IEditorConsoleService consoleService)
	{
		_consoleService = consoleService;
		_consoleService.NewLogEvent += HandleNewLogEvent;
	}

	public override void Dispose()
	{
		base.Dispose();
		_consoleService.NewLogEvent -= HandleNewLogEvent;
	}

	private void HandleNewLogEvent(LogMessage message)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			Logs.Add(new ConsoleLogMessageViewModel(message));
			NewLogAddedEvent?.Invoke(message);
		});
	}
}