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
    private readonly Queue<(LogMessage? Message, int Count)> _pendingNotifications = new();
    private bool _publishing;

    public void Log(LogMessage message)
    {
        lock (_sync)
        {
            _logs.Add(message);
            _pendingNotifications.Enqueue((message, _logs.Count));
            if (_publishing) return;
            _publishing = true;
        }

        PublishPendingNotifications();
    }

    public void ClearConsole()
    {
        lock (_sync)
        {
            if (_logs.Count == 0) return;
            _logs.Clear();
            _pendingNotifications.Enqueue((null, 0));
            if (_publishing) return;
            _publishing = true;
        }

        PublishPendingNotifications();
    }

    private void PublishPendingNotifications()
    {
        try
        {
            while (true)
            {
                (LogMessage? Message, int Count) notification;
                lock (_sync)
                {
                    if (!_pendingNotifications.TryDequeue(out notification))
                    {
                        _publishing = false;
                        return;
                    }
                }

                _logsCount.Value = notification.Count;
                if (notification.Message == null) LogsClearedEvent?.Invoke();
                else NewLogEvent?.Invoke(notification.Message);
            }
        }
        catch
        {
            lock (_sync) _publishing = false;
            throw;
        }
    }
}
