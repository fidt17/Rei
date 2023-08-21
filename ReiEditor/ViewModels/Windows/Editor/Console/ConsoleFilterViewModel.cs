using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Logging;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleFilterViewModel : BaseViewModel
{
	public event Action? FilterChangedEvent;
	
	#region InfoEnabled

	private bool _infoEnabled = true;
	public bool InfoEnabled
	{
		get => _infoEnabled;
		set
		{
			if (SetField(ref _infoEnabled, value))
			{
				FilterChangedEvent?.Invoke();
			}
		}
	}

	#endregion
	
	#region WarningEnabled

	private bool _warningEnabled = true;
	public bool WarningEnabled
	{
		get => _warningEnabled;
		set
		{
			if (SetField(ref _warningEnabled, value))
			{
				FilterChangedEvent?.Invoke();
			}
		}
	}

	#endregion
	
	#region ErrorEnabled

	private bool _errorEnabled = true;
	public bool ErrorEnabled
	{
		get => _errorEnabled;
		set
		{
			if (SetField(ref _errorEnabled, value))
			{
				FilterChangedEvent?.Invoke();
			}
		}
	}

	#endregion

	public bool IsValidLog(LogMessage logMessage)
	{
		if (logMessage.LogLevel == LogLevelEnum.Info && InfoEnabled) return true;
		if (logMessage.LogLevel == LogLevelEnum.Warning && WarningEnabled) return true;
		if (logMessage.LogLevel == LogLevelEnum.Error && ErrorEnabled) return true;
		
		return false;
	}

	public IEnumerable<LogMessage> FilterMessages(IEnumerable<LogMessage> messages)
	{
		foreach (var logMessage in messages)
		{
			if (IsValidLog(logMessage))
			{
				yield return logMessage;
			}
		}
	}
}