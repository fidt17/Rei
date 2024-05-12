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
        for (const auto& window : _windows)
        {
            window->OnUpdate();
        }

        glfwPollEvents();
    }

    Window& WindowManager::NewWindow(const std::string& name, const int width, const int height)
    {
        _windows.emplace_back(std::make_shared<Window>(name, width, height));
        const auto w = _windows.back();

        //w->WindowClosed += std::make_shared<std::function<void(Window&)>>([this](const Window& closeWindow) { HandleWindowClosedEvent(closeWindow); });

        return *w;
    }

    void WindowManager::HandleWindowClosedEvent(const Window& w)
    {
        _windows.erase(std::remove_if(_windows.begin(), _windows.end(), [&](const std::shared_ptr<Window>& checkWindow)
        {
            return *checkWindow == w;
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
        glfwTerminate();
    }
}
