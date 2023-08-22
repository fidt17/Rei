using ReiEditor.Models.Services.Logging;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleLogMessageViewModel : BaseViewModel
{
	public string Message { get; }
	public LogLevelEnum LogLevel { get; }
	public string Details { get; }
	
	#region DisplayMessage

	private string _displayMessage = "";
	public string DisplayMessage
	{
		get => _displayMessage;
		private set => SetField(ref _displayMessage, value);
	}

	#endregion
	
	#region Expand

	private bool _expand;
	public bool Expand
	{
		get => _expand;
		set
		{
			if (SetField(ref _expand, value))
			{
				UpdateDisplayMessage();
			}
		}
	}

	#endregion
	
#pragma warning disable CS8618
	public ConsoleLogMessageViewModel() { }
#pragma warning restore CS8618

	public ConsoleLogMessageViewModel(LogMessage message)
	{
		Message = $"[{message.Time.Hour}:{message.Time.Minute}:{message.Time.Second}] [{message.Scope}] {message.Message}";
		Details = message.Details;
		LogLevel = message.Level;
		
		UpdateDisplayMessage();
	}

	private void UpdateDisplayMessage()
	{
		if (Expand)
		{
			DisplayMessage = Message + "\n" + Details;
		}
		else
		{
			DisplayMessage = Message;
		}
	}
}