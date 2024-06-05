#include "pch.h"
#include "WindowManager.h"

namespace rei::window
{
    WindowManager::WindowManager()
    {
        if (!glfwInit())
            REI_THROW("GLFW Initialization error")

        glfwSetErrorCallback([](int error_code, const char* description)
        {
            LOG_ERROR("GLFW ERROR. " + STRING(error_code) + " " + description);
        });
    }

    void WindowManager::OnUpdate()
    {
        if (_windows.empty()) return;
        
        for (const auto& window : _windows)
        {
            window->OnUpdate();
        }

        glfwPollEvents();
    }

    std::shared_ptr<Window> WindowManager::NewWindow(const std::string& name, const int width, const int height)
    {
        _windows.emplace_back(std::make_shared<Window>(name, width, height));

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

    void WindowManager::Dispose()
    {
        CloseAll();
    }
}
