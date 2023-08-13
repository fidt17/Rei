using Avalonia.Controls;

namespace Editor.Models.Services.App.MainWindow;

public interface IMainWindowService
{
	Window GetMainWindow();
	void ShowMainWindow(Window window);
}