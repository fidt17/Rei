#include "pch.h"
#include "Input.h"

void rei::input::Input::SetSource(GLFWwindow* source)
{
    _source = source;
}

bool rei::input::Input::KeyPressed(const i32 key) const
{
    if (_source == nullptr) return false;
    
    return glfwGetKey(_source, key);
}

void rei::input::Input::GetCursorPosition(f32& x, f32& y) const
{
    if (_source == nullptr) return;

    double xPos;
    double yPos;
    glfwGetCursorPos(_source, &xPos, &yPos);

    x = xPos;
    y = yPos;
}
