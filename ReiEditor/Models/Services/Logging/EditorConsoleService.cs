using System;

namespace ReiEditor.Models.Services.Logging;

public class EditorConsoleService : IEditorConsoleService
{
	public event Action<LogMessage>? NewLogEvent;
	
	public void Log(LogMessage message)
	{
		NewLogEvent?.Invoke(message);
	}
}