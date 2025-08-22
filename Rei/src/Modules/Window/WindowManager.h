#pragma once
#include "Window.h"

namespace rei::window
{
    class WindowManager
    {
    public:
        void OnUpdate();

        std::shared_ptr<Window> NewWindow(const std::string& name, int width, int height, bool hideByDefault);

        void CloseWindow(Window& w);
        void CloseAll();

    private:
        std::vector<std::shared_ptr<Window>> _windows;
    };
}
