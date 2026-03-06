using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.SettingsWindow;

public interface ISettingsWindowService
{
	IObservable<bool> IsOpened { get; }
	
	void OpenSettingsWindow();
	void CloseSettingsWindow();
}