#pragma once

#define GLFW_EXPOSE_NATIVE_WIN32
#include "glfw/glfw3.h"
#include "glfw/glfw3native.h"

namespace rei::render
{
    class Renderer
    {
    public:
        void SetTarget(GLFWwindow* target);
        void Render();
        
    private:
        GLFWwindow* _target = nullptr;

        float r = 0;
        float g = 0;
        float b = 0;
        float timeScale = 0.001f;
        float rSpeed = 1 * timeScale;
        float gSpeed = 1.1f * timeScale;
        float bSpeed = 1.2f * timeScale;
    };
}
