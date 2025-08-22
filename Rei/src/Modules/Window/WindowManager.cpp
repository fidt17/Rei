#include "pch.h"
#include "WindowManager.h"

namespace rei::window
{
    void WindowManager::OnUpdate()
    {
        if (_windows.empty()) return;

        glfwPollEvents();
        
        for (const auto& window : _windows)
        {
            window->OnUpdate();
        }
    }

    std::shared_ptr<Window> WindowManager::NewWindow(const std::string& name, const int width, const int height, const bool hideByDefault)
    {
        _windows.emplace_back(std::make_shared<Window>(name, width, height, hideByDefault));

        return _windows.back();
    }

    void WindowManager::CloseWindow(Window& w)
    {
        w.Close();

        _windows.erase(std::find_if(_windows.begin(), _windows.end(), [&](const std::shared_ptr<Window>& other)
        {
            return *other == w;
        }), _windows.end());
    }

    void WindowManager::CloseAll()
    {
        for (const auto& window : _windows)
        {
            window->Close();
        }
        _windows.clear();
    }
}
