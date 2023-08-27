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

	private ConsoleLogMessageViewModel? _currentlyExpandedLog;
	
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
				FilteredLogs.Add(CreateLogVm(message));
			}
			Logs.Add(message);
			LogCollectionUpdated?.Invoke();
		});
	}

	private void ClearLogs()
	{
		foreach (var vm in FilteredLogs)
		{
			vm.Dispose();
		}
		FilteredLogs.Clear();
	}

	private void RebuildLogCollection()
	{
		ClearLogs();
		
		var logs = ConsoleFilter.FilterMessages(Logs).Select(CreateLogVm);
		FilteredLogs.AddRange(logs);
		
		LogCollectionUpdated?.Invoke();
	}

	private ConsoleLogMessageViewModel CreateLogVm(LogMessage log)
	{
		var vm = new ConsoleLogMessageViewModel(log);
		vm.DetailsExpandedEvent += logVm =>
		{
			if (_currentlyExpandedLog != null)
			{
				_currentlyExpandedLog.Expand = false;
			}

			_currentlyExpandedLog = logVm;
		};

		return vm;
	}

	private void HandleFilterChangedEvent() => RebuildLogCollection();
}