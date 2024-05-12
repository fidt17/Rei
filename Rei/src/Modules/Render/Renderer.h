#pragma once

#define GLFW_EXPOSE_NATIVE_WIN32
#include "GLFW/glfw3.h"
#include "GLFW/glfw3native.h"

namespace rei::render
{
    class Renderer
    {
    public:
        void SetupWindow(int width, int height, const std::string& name);

        void Render();

        void Terminate();

        REI_API HWND GetWindowHandler() const;

    private:
        GLFWwindow* _window = nullptr;

        float r = 0;
        float g = 0;
        float b = 0;
        float timeScale = 0.001f;
        float rSpeed = 1 * timeScale;
        float gSpeed = 1.1f * timeScale;
        float bSpeed = 1.2f * timeScale;
    };
}
