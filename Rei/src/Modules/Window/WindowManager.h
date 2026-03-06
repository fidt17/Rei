#pragma once
#include "Window.h"

namespace rei::window
{
    class WindowManager
    {
    public:
        WindowManager();
        void OnUpdate() const;

        std::shared_ptr<Window> NewWindow(const WindowCreationSettings& settings);

        void CloseWindow(Window& w);
        void CloseAll();

        void SetCursorIcon(i32 icon) const;
        
    private:
        std::vector<std::shared_ptr<Window>> _windows;
    };
}
