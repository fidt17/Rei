using ReiEditor.Models.Services.Logging;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleLogMessageViewModel : BaseViewModel
{
	public string Message { get; }
	public LogLevelEnum LogLevel { get; }
	
#pragma warning disable CS8618
	public ConsoleLogMessageViewModel() { }
#pragma warning restore CS8618

	public ConsoleLogMessageViewModel(LogMessage message)
	{
		Message = $"[{message.Time.ToShortTimeString()}] {message.Message}";
		LogLevel = message.LogLevel;
	}
}