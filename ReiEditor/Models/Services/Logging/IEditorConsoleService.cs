using System;

namespace ReiEditor.Models.Services.Logging;

public interface IEditorConsoleService
{
	event Action<LogMessage> NewLogEvent;

	void Log(LogMessage message);
}