#include "pch.h"
#include "WindowManager.h"

#include "Modules/Input/Input.h"

namespace rei::window
{
    WindowManager::WindowManager()
    {
        glfwSetErrorCallback([](int error_code, const char* description)
        {
            LOG_ERROR("GLFW ERROR. {} {}", error_code, description)
        });

        if (!glfwInit())
        {
            REI_THROW("GLFW Initialization error")
        }

        glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
        glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
        glfwWindowHint(GLFW_SAMPLES, 4);
        glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
    }

    void WindowManager::OnUpdate() const
    {
        if (_windows.empty()) return;

        Input::Update();
        glfwPollEvents();

        for (const auto& window : _windows)
        {
            window->OnUpdate();
        }
    }

    std::shared_ptr<Window> WindowManager::NewWindow(const WindowCreationSettings& settings)
    {
        _windows.emplace_back(std::make_shared<Window>(settings));

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

    void WindowManager::SetCursorIcon(i32 icon) const
    {
        const auto cursor = glfwCreateStandardCursor(icon);

        for (const auto& window : _windows)
        {
            glfwSetCursor(window->GetGLFWWindow(), cursor);
        }
    }
}
