#include "pch.h"
#include "Api/EditorApi.h"
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

    bool Input::IsKeyDown(const i32 key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return _currentKeyStates[key];
    }

    bool Input::IsKeyUp(const i32 key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return !_currentKeyStates[key];
    }

    bool Input::IsKeyPressed(const i32 key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return _currentKeyStates[key] && !_previousKeyStates[key];
    }

    bool Input::IsKeyReleased(const i32 key)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return false;
        
        return !_currentKeyStates[key] && _previousKeyStates[key];
    }

    bool Input::IsMouseButtonDown(const i32 button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return _currentMouseStates[button];
    }

    bool Input::IsMouseButtonUp(const i32 button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return !_currentMouseStates[button];
    }

    bool Input::IsMouseButtonPressed(const i32 button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return _currentMouseStates[button] && !_previousMouseStates[button];
    }

    bool Input::IsMouseButtonReleased(const i32 button)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return false;
        
        return !_currentMouseStates[button] && _previousMouseStates[button];
    }

    f64 Input::GetMouseX()
    {
        return _mouseX;
    }

    f64 Input::GetMouseY()
    {
        return _mouseY;
    }

    void Input::GetMousePosition(f32& x, f32& y)
    {
        x = _mouseX;
        y = _mouseY;
    }

    f64 Input::GetScrollX()
    {
        return _scrollX;
    }

    f64 Input::GetScrollY()
    {
        return _scrollY;
    }

    void Input::KeyCallback(GLFWwindow* window, const i32 key, i32 scancode, const i32 action, i32 mods)
    {
        if (key < 0 || key >= GLFW_KEY_LAST) return;

        if (action == GLFW_PRESS)
        {
            _currentKeyStates[key] = true;
            api::EditorInputEvent inputEvent;
            inputEvent.Type = api::EditorInputEventType::KeyDown;
            inputEvent.Code = key;
            inputEvent.Mods = mods;
            inputEvent.MouseX = _mouseX;
            inputEvent.MouseY = _mouseY;
            GetEditorEventsRelay().EditorInputReceivedEvent(inputEvent);
        }
        else if (action == GLFW_RELEASE)
        {
            _currentKeyStates[key] = false;
            api::EditorInputEvent inputEvent;
            inputEvent.Type = api::EditorInputEventType::KeyUp;
            inputEvent.Code = key;
            inputEvent.Mods = mods;
            inputEvent.MouseX = _mouseX;
            inputEvent.MouseY = _mouseY;
            GetEditorEventsRelay().EditorInputReceivedEvent(inputEvent);
        }
    }

    void Input::MouseButtonCallback(GLFWwindow* window, const i32 button, const i32 action, i32 mods)
    {
        if (button < 0 || button >= GLFW_MOUSE_BUTTON_LAST) return;

        if (action == GLFW_PRESS)
        {
            _currentMouseStates[button] = true;
            api::EditorInputEvent inputEvent;
            inputEvent.Type = api::EditorInputEventType::MouseButtonDown;
            inputEvent.Code = button;
            inputEvent.Mods = mods;
            inputEvent.MouseX = _mouseX;
            inputEvent.MouseY = _mouseY;
            GetEditorEventsRelay().EditorInputReceivedEvent(inputEvent);
        }
        else if (action == GLFW_RELEASE)
        {
            _currentMouseStates[button] = false;
            api::EditorInputEvent inputEvent;
            inputEvent.Type = api::EditorInputEventType::MouseButtonUp;
            inputEvent.Code = button;
            inputEvent.Mods = mods;
            inputEvent.MouseX = _mouseX;
            inputEvent.MouseY = _mouseY;
            GetEditorEventsRelay().EditorInputReceivedEvent(inputEvent);
        }
    }

    void Input::MousePositionCallback(GLFWwindow* window, const f64 xPos, const f64 yPos)
    {
        _mouseX = static_cast<f32>(xPos);
        _mouseY = static_cast<f32>(yPos);
    }

    void Input::ScrollCallback(GLFWwindow* window, const f64 xOffset, const f64 yOffset)
    {
        _scrollX = static_cast<f32>(xOffset);
        _scrollY = static_cast<f32>(yOffset);
    }
}
