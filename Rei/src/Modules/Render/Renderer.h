#pragma once

#define GLFW_EXPOSE_NATIVE_WIN32
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "glfw/glfw3native.h"

namespace rei::render
{
    class Renderer
    {
    public:
        Renderer() = default;
        void SetTarget(GLFWwindow* target);
        void Render() const;

        void Dispose();
    private:
        GLFWwindow* _target = nullptr;
    };
}
