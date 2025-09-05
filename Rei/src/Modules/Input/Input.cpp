#include "pch.h"
#include "Input.h"

namespace rei
{
    void Input::SetSource(GLFWwindow* window)
    {
        _window = window;

        glfwSetKeyCallback(window, KeyCallback);
        glfwSetMouseButtonCallback(window, MouseButtonCallback);
        glfwSetCursorPosCallback(window, MousePositionCallback);
        glfwSetScrollCallback(window, ScrollCallback);

        _currentKeyStates.fill(false);
        _previousKeyStates.fill(false);
        _currentMouseStates.fill(false);
        _previousMouseStates.fill(false);
    }

    void Input::Update()
    {
        _previousKeyStates = _currentKeyStates;
        _previousMouseStates = _currentMouseStates;

        _scrollX = 0.0;
        _scrollY = 0.0;
    }

    bool Input::IsKeyDown(const int key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return _currentKeyStates[key];
    }

    bool Input::IsKeyUp(const int key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return !_currentKeyStates[key];
    }

    bool Input::IsKeyPressed(const int key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return _currentKeyStates[key] && !_previousKeyStates[key];
    }

    bool Input::IsKeyReleased(const int key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return !_currentKeyStates[key] && _previousKeyStates[key];
    }

    bool Input::IsMouseButtonDown(const int button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return _currentMouseStates[button];
    }

    bool Input::IsMouseButtonUp(const int button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return !_currentMouseStates[button];
    }

    bool Input::IsMouseButtonPressed(const int button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return _currentMouseStates[button] && !_previousMouseStates[button];
    }

    bool Input::IsMouseButtonReleased(const int button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return !_currentMouseStates[button] && _previousMouseStates[button];
    }

    double Input::GetMouseX()
    {
        return _mouseX;
    }

    double Input::GetMouseY()
    {
        return _mouseY;
    }

    void Input::GetMousePosition(f32& x, f32& y)
    {
        x = _mouseX;
        y = _mouseY;
    }

    double Input::GetScrollX()
    {
        return _scrollX;
    }

    double Input::GetScrollY()
    {
        return _scrollY;
    }

    void Input::KeyCallback(GLFWwindow* window, const int key, int scancode, const int action, int mods)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return;

        if (action == GLFW_PRESS)
        {
            _currentKeyStates[key] = true;
        }
        else if (action == GLFW_RELEASE)
        {
            _currentKeyStates[key] = false;
        }
    }

    void Input::MouseButtonCallback(GLFWwindow* window, const int button, const int action, int mods)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return;

        if (action == GLFW_PRESS)
        {
            _currentMouseStates[button] = true;
        }
        else if (action == GLFW_RELEASE)
        {
            _currentMouseStates[button] = false;
        }
    }

    void Input::MousePositionCallback(GLFWwindow* window, const double xpos, const double ypos)
    {
        _mouseX = xpos;
        _mouseY = ypos;
    }

    void Input::ScrollCallback(GLFWwindow* window, const double xoffset, const double yoffset)
    {
        _scrollX = xoffset;
        _scrollY = yoffset;
    }
}
