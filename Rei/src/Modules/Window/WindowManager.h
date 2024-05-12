#pragma once
#include "Window.h"

namespace rei::window
{
    class WindowManager
    {
    public:
        WindowManager();
        void OnUpdate();

        Window& NewWindow(const std::string& name, int width, int height);
        void HandleWindowClosedEvent(const Window& w);

        void CloseAll();
        void Dispose();

    private:
        std::vector<std::shared_ptr<Window>> _windows;
    };
}
