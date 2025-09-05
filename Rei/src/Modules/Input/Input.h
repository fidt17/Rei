#pragma once
#include "GLFW/glfw3.h"

namespace rei
{
    class Input {
    public:
        static void SetSource(GLFWwindow* window);
        static void Update();
    
        REI_API static bool IsKeyDown(int key);
        REI_API static bool IsKeyUp(int key);
        REI_API static bool IsKeyPressed(int key);
        REI_API static bool IsKeyReleased(int key);
    
        REI_API static bool IsMouseButtonDown(int button);
        REI_API static bool IsMouseButtonUp(int button);
        REI_API static bool IsMouseButtonPressed(int button);
        REI_API static bool IsMouseButtonReleased(int button);
    
        REI_API static double GetMouseX();
        REI_API static double GetMouseY();
        REI_API static void GetMousePosition(f32& x, f32& y);
    
        REI_API static double GetScrollX();
        REI_API static double GetScrollY();

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
    
        static void KeyCallback(GLFWwindow* window, int key, int scancode, int action, int mods);
        static void MouseButtonCallback(GLFWwindow* window, int button, int action, int mods);
        static void MousePositionCallback(GLFWwindow* window, double xpos, double ypos);
        static void ScrollCallback(GLFWwindow* window, double xoffset, double yoffset);
    }; 
}
