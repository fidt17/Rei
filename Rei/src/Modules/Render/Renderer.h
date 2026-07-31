#pragma once

#define GLFW_EXPOSE_NATIVE_WIN32
#include "Ecs/ComponentRef.h"
#include "RenderScenario/BaseRenderScenario.h"

namespace rei::render
{
    class Renderer
    {
    public:
        Renderer() = default;

        void SetCamera(const ecs::ComponentRef<Camera>& camera);
        ecs::ComponentRef<Camera> GetCamera() const;

        void SetTarget(GLFWwindow* target);

        void Render() const;
        bool RequestFrameCapture(const FrameCaptureCallback& callback) const;

        void Dispose();

    private:
        GLFWwindow* _target = nullptr;
        ecs::ComponentRef<Camera> _camera;
        std::unique_ptr<BaseRenderScenario> _renderScenario = nullptr;

        void PrepareAssets() const;
    };
}
