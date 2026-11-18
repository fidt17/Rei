#include "pch.h"
#include "Api/EditorApi.h"
#include "Input.h"
#include <cmath>

namespace rei
{
    void Input::SetSource(GLFWwindow* window)
    {
        CancelSimulation();
        _window = window;
        ++_sourceRevision;

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

    MouseState Input::GetMouseState()
    {
        MouseState state;
        if (_window == nullptr || glfwGetCurrentContext() != _window) return state;
        state.X = _mouseX;
        state.Y = _mouseY;
        glfwGetWindowSize(_window, &state.Width, &state.Height);
        state.HasFocus = glfwGetWindowAttrib(_window, GLFW_FOCUSED) == GLFW_TRUE;
        state.IsHovered = glfwGetWindowAttrib(_window, GLFW_HOVERED) == GLFW_TRUE;
        return state;
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

    void Input::StartSimulation(const std::shared_ptr<SimulatedInput>& input)
    {
        if (_simulation) { input->Completion.set_value(-2); return; }
        if (!_window || input->Cancelled) { input->Completion.set_value(-1); return; }
        glfwGetFramebufferSize(_window, &input->Width, &input->Height);
        if (input->Width <= 0 || input->Height <= 0 || input->Events.empty() || input->Events.size() > 128)
        { input->Completion.set_value(-3); return; }
        for (const auto& event : input->Events)
        {
            const bool pointer = event.Kind == SimulatedInputEvent::Move;
            const bool button = event.Kind == SimulatedInputEvent::ButtonDown || event.Kind == SimulatedInputEvent::ButtonUp;
            const bool key = event.Kind == SimulatedInputEvent::KeyDown || event.Kind == SimulatedInputEvent::KeyUp;
            if (!std::isfinite(event.X) || !std::isfinite(event.Y) ||
                (pointer && (event.X < 0 || event.Y < 0 || event.X >= input->Width || event.Y >= input->Height)) ||
                (button && (event.Code < 0 || event.Code >= GLFW_MOUSE_BUTTON_LAST)) ||
                (key && (event.Code < 0 || event.Code >= GLFW_KEY_LAST)))
            { input->Completion.set_value(-3); return; }
        }
        _simulation = input;
    }

    i32 Input::GetModifiers()
    {
        i32 mods = 0;
        if (IsKeyDown(GLFW_KEY_LEFT_SHIFT) || IsKeyDown(GLFW_KEY_RIGHT_SHIFT)) mods |= GLFW_MOD_SHIFT;
        if (IsKeyDown(GLFW_KEY_LEFT_CONTROL) || IsKeyDown(GLFW_KEY_RIGHT_CONTROL)) mods |= GLFW_MOD_CONTROL;
        if (IsKeyDown(GLFW_KEY_LEFT_ALT) || IsKeyDown(GLFW_KEY_RIGHT_ALT)) mods |= GLFW_MOD_ALT;
        if (IsKeyDown(GLFW_KEY_LEFT_SUPER) || IsKeyDown(GLFW_KEY_RIGHT_SUPER)) mods |= GLFW_MOD_SUPER;
        return mods;
    }

    void Input::FinishSimulation(const i32 result)
    {
        if (!_simulation) return;
        for (const auto code : _simulation->Buttons) MouseButtonCallback(_window, code, GLFW_RELEASE, GetModifiers());
        for (const auto code : _simulation->Keys) KeyCallback(_window, code, 0, GLFW_RELEASE, GetModifiers());
        _simulation->Completion.set_value(result);
        _simulation.reset();
    }

    void Input::CancelSimulation() { FinishSimulation(-1); }

    void Input::StepSimulation()
    {
        if (!_simulation) return;
        if (_simulation->Cancelled) { FinishSimulation(-4); return; }
        if (_simulation->Next == _simulation->Events.size())
        {
            // Release unbalanced sequences, then give gizmos one frame to commit.
            if (!_simulation->Buttons.empty() || !_simulation->Keys.empty())
            {
                for (const auto code : _simulation->Buttons) MouseButtonCallback(_window, code, GLFW_RELEASE, GetModifiers());
                for (const auto code : _simulation->Keys) KeyCallback(_window, code, 0, GLFW_RELEASE, GetModifiers());
                _simulation->Buttons.clear();
                _simulation->Keys.clear();
                return;
            }
            FinishSimulation(1);
            return;
        }
        const auto& event = _simulation->Events[_simulation->Next++];
        switch (event.Kind)
        {
            case SimulatedInputEvent::Move:
            {
                i32 width = 0, height = 0;
                glfwGetWindowSize(_window, &width, &height);
                MousePositionCallback(_window, event.X * width / _simulation->Width, event.Y * height / _simulation->Height);
                break;
            }
            case SimulatedInputEvent::ButtonDown:
                _simulation->Buttons.insert(event.Code);
                MouseButtonCallback(_window, event.Code, GLFW_PRESS, GetModifiers());
                break;
            case SimulatedInputEvent::ButtonUp:
                _simulation->Buttons.erase(event.Code);
                MouseButtonCallback(_window, event.Code, GLFW_RELEASE, GetModifiers());
                break;
            case SimulatedInputEvent::KeyDown:
                _simulation->Keys.insert(event.Code);
                KeyCallback(_window, event.Code, 0, GLFW_PRESS, GetModifiers());
                break;
            case SimulatedInputEvent::KeyUp:
                _simulation->Keys.erase(event.Code);
                KeyCallback(_window, event.Code, 0, GLFW_RELEASE, GetModifiers());
                break;
            case SimulatedInputEvent::Scroll: ScrollCallback(_window, event.X, event.Y); break;
            case SimulatedInputEvent::Wait: break;
        }
    }
}
