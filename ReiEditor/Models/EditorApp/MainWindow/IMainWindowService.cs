using Avalonia.Controls;

namespace ReiEditor.Models.EditorApp.MainWindow;

public interface IMainWindowService
{
	Window GetMainWindow();
	void ShowMainWindow(Window window);
}