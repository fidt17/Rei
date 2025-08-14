#include "pch.h"
#include "MainWindowHandler.h"

namespace rei::window
{
    std::shared_ptr<Window> MainWindowHandler::CreateMainWindow(WindowManager& windowManager)
    {
        REI_ASSERT_S(_window == nullptr)

        _window = windowManager.NewWindow("Main Window", 900, 900);

        _window->OnKeyEvent.append([&](const int key, const int, const int)
        {
            if (key != GLFW_KEY_ESCAPE) return;

            windowManager.CloseWindow(*_window);
        });

        _window->CloseRequestEvent.append([&]
        {
            windowManager.CloseWindow(*_window);
        });

        _window->WindowClosedEvent.append([&](const Window&)
        {
            MainWindowClosedEvent();
        });

        return _window;
    }

    std::shared_ptr<Window> MainWindowHandler::GetMainWindow()
    {
        return _window;
    }

    bool MainWindowHandler::IsSet() const
    {
        return _window != nullptr;
    }
}
