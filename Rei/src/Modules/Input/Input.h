#pragma once
#include "GLFW/glfw3.h"

namespace rei
{
    class Input {
    public:
        static void SetSource(GLFWwindow* window);
        static void Update();
    
        REI_API static bool IsKeyDown(i32 key);
        REI_API static bool IsKeyUp(i32 key);
        REI_API static bool IsKeyPressed(i32 key);
        REI_API static bool IsKeyReleased(i32 key);
    
        REI_API static bool IsMouseButtonDown(i32 button);
        REI_API static bool IsMouseButtonUp(i32 button);
        REI_API static bool IsMouseButtonPressed(i32 button);
        REI_API static bool IsMouseButtonReleased(i32 button);
    
        REI_API static f64 GetMouseX();
        REI_API static f64 GetMouseY();
        REI_API static void GetMousePosition(f32& x, f32& y);
    
        REI_API static f64 GetScrollX();
        REI_API static f64 GetScrollY();

    private:
        inline static GLFWwindow* _window = nullptr;

        inline static std::array<bool, GLFW_KEY_LAST> _currentKeyStates { };
        inline static std::array<bool, GLFW_KEY_LAST> _previousKeyStates { };

        inline static std::array<bool, GLFW_MOUSE_BUTTON_LAST> _currentMouseStates { };
        inline static std::array<bool, GLFW_MOUSE_BUTTON_LAST> _previousMouseStates { };

        inline static f32 _mouseX = 0;
        inline static f32 _mouseY = 0;

        inline static f32 _scrollX = 0;
        inline static f32 _scrollY = 0;
    
        static void KeyCallback(GLFWwindow* window, i32 key, i32 scancode, i32 action, i32 mods);
        static void MouseButtonCallback(GLFWwindow* window, i32 button, i32 action, i32 mods);
        static void MousePositionCallback(GLFWwindow* window, f64 xPos, f64 yPos);
        static void ScrollCallback(GLFWwindow* window, f64 xOffset, f64 yOffset);
    }; 
}
