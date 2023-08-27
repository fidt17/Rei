using System;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleLogMessageViewModel : BaseViewModel
{
	public event Action<ConsoleLogMessageViewModel>? DetailsExpandedEvent;
	
	public RelayCommand ExpandContentsCommand { get; }
	
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
				ExpandContentsCommand.InvokeCanExecuteChanged();

				if (value)
				{
					DetailsExpandedEvent?.Invoke(this);
				}
			}
		}
	}

	#endregion
	
#pragma warning disable CS8618
	public ConsoleLogMessageViewModel() { }
#pragma warning restore CS8618

	public ConsoleLogMessageViewModel(LogMessage message)
	{
		Message = $"[{message.Time.Hour:00}:{message.Time.Minute:00}:{message.Time.Second:00}] {message.Message}";
		Details = $"{message.Scope}\n{message.Details}";
		LogLevel = message.Level;
		
		UpdateDisplayMessage();

		ExpandContentsCommand = new RelayCommand(() =>
		{
			Expand = true;
		}, () => !Expand);
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