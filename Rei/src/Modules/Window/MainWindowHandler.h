#pragma once
#include "WindowManager.h"

class MainWindowHandler
{
public:
    eventpp::CallbackList<void()> MainWindowClosedEvent;

    std::shared_ptr<rei::window::Window> CreateMainWindow(rei::window::WindowManager& windowManager)
    {
        _window = windowManager.NewWindow("Main Window", 400, 400);

        _window->OnKeyEvent.append([&](const int key, const int, const int)
        {
            if (key != GLFW_KEY_ESCAPE) return;

            windowManager.CloseWindow(*_window);
        });

        _window->CloseRequestEvent.append([&]
        {
            windowManager.CloseWindow(*_window);
        });

        _window->WindowClosedEvent.append([&](const rei::window::Window&)
        {
            MainWindowClosedEvent();
        });

        return _window;
    }

private:
    std::shared_ptr<rei::window::Window> _window;
};
