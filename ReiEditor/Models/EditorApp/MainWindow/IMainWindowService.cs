using System;
using Avalonia.Controls;

namespace ReiEditor.Models.EditorApp.MainWindow;

public interface IMainWindowService
{
    event Action ActivatedEvent;
    event Action DeactivatedEvent;
	
    Window GetMainWindow();
    void ShowMainWindow(Window window);

    void ShowDialog(Window window);
}
