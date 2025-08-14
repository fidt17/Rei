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

        bool IsSet() const;

    private:
        std::shared_ptr<Window> _window;
    };
}
