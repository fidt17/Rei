using System;
using System.Collections.Generic;

namespace ReiEditor.Models.Services.Logging.EditorConsole;

public interface IEditorConsoleService
{
	event Action<LogMessage> NewLogEvent;
	event Action LogsClearedEvent;
	
	Utils.Common.IObservable<int> LogsCount { get; }
	IEnumerable<LogMessage> Logs { get; }

	void Log(LogMessage message);
	void ClearConsole();
}