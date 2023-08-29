using System;

namespace ReiEditor.Models.EditorApp.SettingsWindow;

public interface ISettingsWindowService
{
	event Action<bool> IsOpenedValueChangedEvent;
	
	bool IsOpened { get; }
	void OpenSettingsWindow();
	void CloseSettingsWindow();
}