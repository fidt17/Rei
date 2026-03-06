using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    }

    public override void Dispose()
    {
        base.Dispose();
        _consoleService.NewLogEvent -= HandleNewLogEvent;
        ClearEditorConsoleCommand.Dispose();

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
            LogCollectionUpdated?.Invoke();
        });
    }

    private void HandleLogsClearedEvent() => ClearLogs();

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
        ClearLogs();
		
        var logs = ConsoleFilter.FilterMessages(_consoleService.Logs).Select(CreateLogVm);
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
            Details = _currentlyExpandedLog.Message + " \nDetails:\n" + _currentlyExpandedLog.Details;
        };

        return vm;
    }

    private void HandleFilterChangedEvent() => RebuildLogCollection();
}