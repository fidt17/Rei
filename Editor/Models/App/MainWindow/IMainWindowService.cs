using Avalonia.Controls;

namespace Editor.Models.App.MainWindow;

public interface IMainWindowService
{
	Window GetMainWindow();
	void ShowMainWindow(Window window);
}