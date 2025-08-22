#pragma once
#include "WindowManager.h"

namespace rei::window
{
    class MainWindowHandler
    {
    public:
        eventpp::CallbackList<void()> MainWindowClosedEvent;

        std::shared_ptr<Window> CreateMainWindow(WindowManager& windowManager, i32 width, i32 height, bool hideByDefault);

        std::shared_ptr<Window> GetMainWindow();

        bool IsSet() const;

    private:
        std::shared_ptr<Window> _window;
    };
}
