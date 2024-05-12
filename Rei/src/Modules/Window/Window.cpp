#include "pch.h"
#include "Window.h"

namespace rei::window
{
    Window::Window(const std::string& name, const int width, const int height)
    {
        _glfwWindow = glfwCreateWindow(width, height, name.c_str(), nullptr, nullptr);
        REI_THROW_IF(!_glfwWindow, "Window creation failed")

        glfwSetWindowUserPointer(_glfwWindow, this);

        glfwSetKeyCallback(_glfwWindow, [](GLFWwindow* w, const int key, int, const int action, const int mods)
        {
            static_cast<Window*>(glfwGetWindowUserPointer(w))->OnKeyCallback(key, action, mods);
        });
        
        // set window style
        //SetWindowLongPtr(glfwGetWin32Window(_window), GWL_STYLE, 0);
    }

    void Window::OnUpdate()
    {
        REI_ASSERT_NOT_NULL(_glfwWindow);
        
        if (glfwWindowShouldClose(_glfwWindow))
        {
            Close();
        }
    }

    void Window::Close()
    {
        REI_ASSERT_NOT_NULL(_glfwWindow);
        
        glfwDestroyWindow(_glfwWindow);
        _glfwWindow = nullptr;

        WindowClosed.Invoke(*this);
    }

    GLFWwindow* Window::GetGLFWWindow() const
    {
        REI_ASSERT_NOT_NULL(_glfwWindow);
        
        return _glfwWindow;
    }

    HWND Window::GetWindowHandler() const
    {
        REI_ASSERT_NOT_NULL(_glfwWindow)
        return glfwGetWin32Window(_glfwWindow);
    }

    void Window::OnKeyCallback(const int key, const int action, const int mods)
    {
        LOG("key: " + STRING(key) + " action: " + STRING(action) + " scancode: " + STRING(mods))

        if (key == GLFW_KEY_ESCAPE)
        {
            Close();
        }
    }
}
