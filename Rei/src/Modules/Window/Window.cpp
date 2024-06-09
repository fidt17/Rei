#include "pch.h"
#include "Window.h"

namespace rei::window
{
    Window::Window(const std::string& name, const int width, const int height)
    {
        _glfwWindow = glfwCreateWindow(width, height, name.c_str(), nullptr, nullptr);
        REI_THROW_IF(!_glfwWindow, "Window creation failed")

        glfwSetWindowUserPointer(_glfwWindow, this);
        glfwSetWindowSizeCallback(_glfwWindow, [](GLFWwindow* w, const int newWidth, const int newHeight)
        {
            static_cast<Window*>(glfwGetWindowUserPointer(w))->OnWindowResized(newWidth, newHeight);
        });

        glfwSetKeyCallback(_glfwWindow, [](GLFWwindow* w, const int key, int, const int action, const int mods)
        {
            static_cast<Window*>(glfwGetWindowUserPointer(w))->OnKeyCallback(key, action, mods);
        });
    }

    void Window::OnUpdate()
    {
        REI_ASSERT_NOT_NULL(_glfwWindow);

        if (glfwWindowShouldClose(_glfwWindow))
        {
            CloseRequestEvent();
        }
    }

    void Window::Close()
    {
        if (_glfwWindow == nullptr) return;
        
        glfwDestroyWindow(_glfwWindow);
        _glfwWindow = nullptr;

        WindowClosedEvent(*this);
    }

    void Window::DisableStyle() const
    {
        SetWindowLongPtr(glfwGetWin32Window(_glfwWindow), GWL_STYLE, 0);
    }

    void Window::Resize(const int width, const int height) const
    {
        glfwSetWindowSize(_glfwWindow, width, height);
    }

    GLFWwindow* Window::GetGLFWWindow() const
    {
        REI_ASSERT_NOT_NULL(_glfwWindow);
        
        return _glfwWindow;
    }

    HWND Window::GetWindowHandle() const
    {
        REI_ASSERT_NOT_NULL(_glfwWindow)
        return glfwGetWin32Window(_glfwWindow);
    }

    void Window::OnKeyCallback(const int key, const int action, const int mods) const
    {
        //LOG("key: " + STRING(key) + " action: " + STRING(action) + " scancode: " + STRING(mods))
        OnKeyEvent(key, action, mods);
    }

    // TODO: probably should be handled by the renderer ?
    void Window::OnWindowResized(const int width, const int height) const
    {
        glViewport(0, 0, width,height);
    }
}
