using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using DynamicData;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Logging;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Console.Commands;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleEditorWindowViewModel : BaseViewModel
{
    public event Action? LogCollectionUpdated;
	
    public ClearEditorConsoleCommand ClearEditorConsoleCommand { get; }
	
    public ObservableCollection<ConsoleLogMessageViewModel> FilteredLogs { get; } = new();

    public ConsoleFilterViewModel ConsoleFilter { get; }
	
    #region Details

    private string _details = "";
    public string Details
    {
        get => _details;
        private set
        {
            if (!SetField(ref _details, value)) return;
            HasDetails = !string.IsNullOrWhiteSpace(value);
        }
    }

    #endregion

    private bool _hasDetails;
    public bool HasDetails
    {
        get => _hasDetails;
        private set => SetField(ref _hasDetails, value);
    }

    private ConsoleLogMessageViewModel? _currentlyExpandedLog;
	
    private readonly IEditorConsoleService _consoleService;
    private int _updateQueued;
    private int _processedLogsCount;
    private int _clearRequested;
    private LogMessage? _lastLog;
    private bool _disposed;

#pragma warning disable CS8618
    public ConsoleEditorWindowViewModel() { }
#pragma warning restore CS8618

    public ConsoleEditorWindowViewModel(IEditorConsoleService consoleService, IEditorConsolePreferencesService editorConsolePreferencesService)
    {
        _consoleService = consoleService;
        _consoleService.NewLogEvent += HandleNewLogEvent;
        _consoleService.LogsClearedEvent += HandleLogsClearedEvent;

        ClearEditorConsoleCommand = new ClearEditorConsoleCommand(consoleService);
		
        ConsoleFilter = new ConsoleFilterViewModel(editorConsolePreferencesService);
        ConsoleFilter.FilterChangedEvent += HandleFilterChangedEvent;
        ScheduleUpdate();
    }

    public override void Dispose()
    {
        _disposed = true;
        base.Dispose();
        _consoleService.NewLogEvent -= HandleNewLogEvent;
        _consoleService.LogsClearedEvent -= HandleLogsClearedEvent;

        ConsoleFilter.FilterChangedEvent -= HandleFilterChangedEvent;
        ConsoleFilter.Dispose();
    }

    private void HandleNewLogEvent(LogMessage message) => ScheduleUpdate();

    private void HandleLogsClearedEvent()
    {
        Interlocked.Exchange(ref _clearRequested, 1);
        ScheduleUpdate();
    }

    private void ScheduleUpdate()
    {
        if (Interlocked.Exchange(ref _updateQueued, 1) != 0) return;
        Dispatcher.UIThread.Post(FlushLogs, DispatcherPriority.Background);
    }

    private void FlushLogs()
    {
        Interlocked.Exchange(ref _updateQueued, 0);
        if (_disposed) return;

        var clearRequested = Interlocked.Exchange(ref _clearRequested, 0) != 0;
        var logs = _consoleService.Logs.ToArray();
        var cleared = clearRequested || logs.Length < _processedLogsCount ||
            (_processedLogsCount > 0 && !ReferenceEquals(logs[_processedLogsCount - 1], _lastLog));
        if (cleared)
        {
            ClearLogs();
            _processedLogsCount = 0;
        }

        if (!cleared && logs.Length == _processedLogsCount) return;

        var added = logs.Skip(_processedLogsCount).Where(ConsoleFilter.IsValidLog).Select(CreateLogVm);
        FilteredLogs.AddRange(added);
        _processedLogsCount = logs.Length;
        _lastLog = logs.LastOrDefault();
        ClearEditorConsoleCommand.NotifyCanExecuteChanged();
        LogCollectionUpdated?.Invoke();
    }

    private void ClearLogs()
    {
        foreach (var vm in FilteredLogs)
        {
            vm.Dispose();
        }
        FilteredLogs.Clear();
        _currentlyExpandedLog = null;
        Details = "";
        HasDetails = false;
    }

    private void RebuildLogCollection()
    {
        Interlocked.Exchange(ref _clearRequested, 0);
        ClearLogs();
		
        var logs = _consoleService.Logs.ToArray();
        FilteredLogs.AddRange(ConsoleFilter.FilterMessages(logs).Select(CreateLogVm));
        _processedLogsCount = logs.Length;
        _lastLog = logs.LastOrDefault();
        ClearEditorConsoleCommand.NotifyCanExecuteChanged();
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
            Details = _currentlyExpandedLog.Message + " \nDetails:\n" + _currentlyExpandedLog.Details;
        };

        return vm;
    }

    private void HandleFilterChangedEvent() => RebuildLogCollection();
}