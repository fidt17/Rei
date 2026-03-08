#pragma once

struct GLFWwindow;

namespace rei::render
{
    class DebugOverlayModule
    {
    public:
        void Setup(GLFWwindow* target);
        void Dispose();
        void Render();

    private:
        GLFWwindow* _target = nullptr;
        bool _isInitialized = false;
    };
}
