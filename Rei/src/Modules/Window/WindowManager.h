#pragma once
#include "Window.h"

namespace rei::window
{
    class WindowManager
    {
    public:
        void OnUpdate();

        std::shared_ptr<Window> NewWindow(const WindowCreationSettings& settings);

        void CloseWindow(Window& w);
        void CloseAll();

    private:
        std::vector<std::shared_ptr<Window>> _windows;
    };
}
