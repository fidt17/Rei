#include "pch.h"
#include "Window.h"

namespace rei::window
{
    Window::Window(const WindowCreationSettings& settings)
    {
        settings.HideOnCreation ? glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE) : glfwWindowHint(GLFW_VISIBLE, GLFW_TRUE);
        GLFWmonitor* monitor = nullptr;
        i32 width = settings.Width;
        i32 height = settings.Height;
        if (settings.FullScreen)
        {
            monitor = glfwGetPrimaryMonitor();
            if (monitor != nullptr)
            {
                const GLFWvidmode* mode = glfwGetVideoMode(monitor);
                if (mode != nullptr)
                {
                    width = mode->width;
                    height = mode->height;
                }
            }
        }

        _glfwWindow = glfwCreateWindow(width, height, settings.Name.c_str(), monitor, nullptr);
        REI_THROW_IF(!_glfwWindow, std::format("Window creation failed"))

        glfwSetWindowUserPointer(_glfwWindow, this);
        glfwSetWindowSizeCallback(_glfwWindow, [](GLFWwindow* w, const int newWidth, const int newHeight)
        {
            static_cast<Window*>(glfwGetWindowUserPointer(w))->OnWindowResized(newWidth, newHeight);
        });

        glfwSetKeyCallback(_glfwWindow, [](GLFWwindow* w, const int key, int, const int action, const int mods)
        {
            static_cast<Window*>(glfwGetWindowUserPointer(w))->OnKeyCallback(key, action, mods);
        });

        settings.HideCursor ? glfwSetInputMode(_glfwWindow, GLFW_CURSOR, GLFW_CURSOR_DISABLED) : glfwSetInputMode(_glfwWindow, GLFW_CURSOR, GLFW_CURSOR_NORMAL);

        if (settings.CenterCursor)
        {
            glfwSetCursorPos(_glfwWindow, width / 2., height / 2.);
        }
        
        glfwSetCursorPosCallback(_glfwWindow, [](GLFWwindow* w, const double xPos, const double yPos)
        {
            static_cast<Window*>(glfwGetWindowUserPointer(w))->OnMouseMoveEvent(xPos, yPos);
        });

        if (!settings.FullScreen)
        {
            CenterWindow();
        }
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
        glViewport(0, 0, width, height);
        SizeChangedEvent(width, height);
    }

    // https://github.com/glfw/glfw/issues/310
    void Window::CenterWindow() const
    {
        int sx = 0, sy = 0;
        int px = 0, py = 0;
        int mx = 0, my = 0;
        int monitor_count = 0;
        int best_area = 0;
        int final_x = 0, final_y = 0;

        glfwGetWindowSize(_glfwWindow, &sx, &sy);
        glfwGetWindowPos(_glfwWindow, &px, &py);

        // Iterate through all monitors
        GLFWmonitor** m = glfwGetMonitors(&monitor_count);
        if (!m) return;

        for (int j = 0; j < monitor_count; ++j)
        {
            glfwGetMonitorPos(m[j], &mx, &my);
            const GLFWvidmode* mode = glfwGetVideoMode(m[j]);
            if (!mode)
                continue;

            // Get intersection of two rectangles - screen and window
            int minX = max(mx, px);
            int minY = max(my, py);

            int maxX = min(mx + mode->width, px + sx);
            int maxY = min(my + mode->height, py + sy);

            // Calculate area of the intersection
            int area = max(maxX - minX, 0) * max(maxY - minY, 0);

            // If its bigger than actual (window covers more space on this monitor)
            if (area > best_area)
            {
                // Calculate proper position in this monitor
                final_x = mx + (mode->width - sx) / 2;
                final_y = my + (mode->height - sy) / 2;

                best_area = area;
            }
        }

        // We found something
        if (best_area)
            glfwSetWindowPos(_glfwWindow, final_x, final_y);

        // Something is wrong - current window has NOT any intersection with any monitors. Move it to the default one.
        else
        {
            GLFWmonitor* primary = glfwGetPrimaryMonitor();
            if (primary)
            {
                const GLFWvidmode* desktop = glfwGetVideoMode(primary);

                if (desktop)
                    glfwSetWindowPos(_glfwWindow, (desktop->width - sx) / 2, (desktop->height - sy) / 2);
            }
        }
    }
}
