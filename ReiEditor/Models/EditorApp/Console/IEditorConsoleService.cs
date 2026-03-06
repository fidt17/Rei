using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Logging;

namespace ReiEditor.Models.EditorApp.Console;

public interface IEditorConsoleService
{
	event Action<LogMessage> NewLogEvent;
	event Action LogsClearedEvent;
	
	Utils.Common.IObservable<int> LogsCount { get; }
	IEnumerable<LogMessage> Logs { get; }

	void Log(LogMessage message);
	void ClearConsole();
}