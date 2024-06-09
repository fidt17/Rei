#pragma once

#define GLFW_EXPOSE_NATIVE_WIN32
#include "GLFW/glfw3.h"
#include "GLFW/glfw3native.h"

namespace rei::window
{
    class Window
    {
    public:
        eventpp::CallbackList<void (Window&)> WindowClosedEvent;
        eventpp::CallbackList<void (int key, int action, int mods)> OnKeyEvent;
        eventpp::CallbackList<void ()> CloseRequestEvent;
        
        Window(const std::string& name, int width, int height);

        friend bool operator==(const Window& lhs, const Window& rhs)
        {
            return lhs._glfwWindow == rhs._glfwWindow;
        }

        friend bool operator!=(const Window& lhs, const Window& rhs)
        {
            return !(lhs == rhs);
        }

        void OnUpdate();

        void Close();
        
        REI_API void DisableStyle() const;
        REI_API void Resize(int width, int height) const;

        GLFWwindow* GetGLFWWindow() const;
        REI_API HWND GetWindowHandle() const;

    private:
        GLFWwindow* _glfwWindow;
        
        void OnKeyCallback(int key, int action, int mods) const;
        void OnWindowResized(int width, int height) const;
    };
}
