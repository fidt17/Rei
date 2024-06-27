#pragma once
#include "GLFW/glfw3.h"

namespace rei::input
{
    class Input
    {
    public:
        void SetSource(GLFWwindow* source);

        bool KeyPressed(i32 key) const;
        void GetCursorPosition(f32& x, f32& y) const;

    private:
        GLFWwindow* _source = nullptr;
    };
}
