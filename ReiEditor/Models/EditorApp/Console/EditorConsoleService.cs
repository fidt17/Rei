using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.Console;

public class EditorConsoleService : IEditorConsoleService
{
    public event Action<LogMessage>? NewLogEvent;
    public event Action? LogsClearedEvent;

    public Utils.Common.IObservable<int> LogsCount => _logsCount;

    public IEnumerable<LogMessage> Logs
    {
        get
        {
            lock (_sync)
            {
                return _logs.ToArray();
            }
        }
    }

    private readonly object _sync = new();
    private readonly List<LogMessage> _logs = new();
    private readonly Observable<int> _logsCount = new(0);

    public void Log(LogMessage message)
    {
        int logsCount;
        lock (_sync)
        {
            _logs.Add(message);
            logsCount = _logs.Count;
        }

        _logsCount.Value = logsCount;
        NewLogEvent?.Invoke(message);
    }

    public void ClearConsole()
    {
        lock (_sync)
        {
            if (_logs.Count == 0) return;
            _logs.Clear();
        }

        _logsCount.Value = 0;
        LogsClearedEvent?.Invoke();
    }
}
