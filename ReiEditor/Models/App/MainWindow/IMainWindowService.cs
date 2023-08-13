using Avalonia.Controls;

namespace ReiEditor.Models.App.MainWindow;

public interface IMainWindowService
{
	Window GetMainWindow();
	void ShowMainWindow(Window window);
}