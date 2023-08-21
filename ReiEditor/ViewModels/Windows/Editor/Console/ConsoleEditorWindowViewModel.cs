using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using DynamicData;
using ReiEditor.Models.Services.Logging;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Console.Commands;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleEditorWindowViewModel : BaseViewModel
{
	public event Action? LogCollectionUpdated;
	
	public ClearConsoleCommand ClearConsoleCommand { get; }
	
	public ObservableCollection<LogMessage> Logs { get; } = new();
	public ObservableCollection<ConsoleLogMessageViewModel> FilteredLogs { get; } = new();

	public ConsoleFilterViewModel ConsoleFilter { get; } = new();

	private readonly IEditorConsoleService _consoleService;

#pragma warning disable CS8618
	public ConsoleEditorWindowViewModel() { }
#pragma warning restore CS8618

	public ConsoleEditorWindowViewModel(IEditorConsoleService consoleService)
	{
		_consoleService = consoleService;
		_consoleService.NewLogEvent += HandleNewLogEvent;

		ClearConsoleCommand = new ClearConsoleCommand(Logs, FilteredLogs);
		ConsoleFilter.FilterChangedEvent += HandleFilterChangedEvent;
	}

	public override void Dispose()
	{
		base.Dispose();
		_consoleService.NewLogEvent -= HandleNewLogEvent;
		ClearConsoleCommand.Dispose();

		ConsoleFilter.FilterChangedEvent -= HandleFilterChangedEvent;
		ConsoleFilter.Dispose();
	}

	private void HandleNewLogEvent(LogMessage message)
	{
		Dispatcher.UIThread.Invoke(() =>
		{
			if (ConsoleFilter.IsValidLog(message))
			{
				FilteredLogs.Add(new ConsoleLogMessageViewModel(message));
			}
			Logs.Add(message);
			LogCollectionUpdated?.Invoke();
		});
	}

	private void RebuildLogCollection()
	{
		FilteredLogs.Clear();
		var logs = ConsoleFilter.FilterMessages(Logs).Select(x => new ConsoleLogMessageViewModel(x));
		FilteredLogs.AddRange(logs);
		
		LogCollectionUpdated?.Invoke();
	}

	private void HandleFilterChangedEvent() => RebuildLogCollection();
}