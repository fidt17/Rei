using System;
using System.Collections.Generic;

namespace ReiEditor.Models.Services.Logging.EditorConsole;

public class EditorConsoleService : IEditorConsoleService
{
	public event Action<LogMessage>? NewLogEvent;
	public event Action? LogsClearedEvent;
	public event Action<int>? LogsCountChangedEvent;

	public int LogsCount => _logs.Count;
	public IEnumerable<LogMessage> Logs => _logs;

	private readonly List<LogMessage> _logs = new();

	public void Log(LogMessage message)
	{
		_logs.Add(message);
		
		NewLogEvent?.Invoke(message);
		LogsCountChangedEvent?.Invoke(LogsCount);
	}

	public void ClearConsole()
	{
		if (LogsCount == 0) return;
		
		_logs.Clear();
		
		LogsClearedEvent?.Invoke();
		LogsCountChangedEvent?.Invoke(LogsCount);
	}
}