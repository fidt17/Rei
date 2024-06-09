#pragma once
#include "WindowManager.h"

namespace rei::window
{
    class MainWindowHandler
    {
    public:
        eventpp::CallbackList<void()> MainWindowClosedEvent;

        std::shared_ptr<Window> CreateMainWindow(WindowManager& windowManager);

        std::shared_ptr<Window> GetMainWindow();

    private:
        std::shared_ptr<Window> _window;
    };
}
