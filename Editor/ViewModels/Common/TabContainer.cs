using System;
using Editor.Models.Services.Logging;

namespace Editor.ViewModels;

public class TabContainer : BaseViewModel
{
	public event Action? TabChangedEvent;

	#region ActiveTab

	private BaseViewModel? _activeTab;

	public BaseViewModel? ActiveTab
	{
		get => _activeTab;
		private set
		{
			if (SetField(ref _activeTab, value))
			{
				TabChangedEvent?.Invoke();
			}
		}
	}

	#endregion

	public void OpenTab(BaseViewModel vm)
	{
		ActiveTab = vm;
	}

	public void LogOnTabChange<T>(ILogger<T> logger)
	{
		TabChangedEvent += () =>
		{
			if (ActiveTab != null)
			{
				logger.Log($"Open tab {ActiveTab.GetType().Name.Replace("ViewModel", "")}");
			}
		};
	}
}