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
	
	public IEnumerable<LogMessage> Logs => _logs;

	private readonly List<LogMessage> _logs = new();
	private readonly Observable<int> _logsCount = new(0);

	public void Log(LogMessage message)
	{
		_logs.Add(message);
		
		NewLogEvent?.Invoke(message);
		_logsCount.Value = _logs.Count;
	}

	public void ClearConsole()
	{
		if (_logs.Count == 0) return;
		
		_logs.Clear();
		
		LogsClearedEvent?.Invoke();
		_logsCount.Value = _logs.Count;
	}
}