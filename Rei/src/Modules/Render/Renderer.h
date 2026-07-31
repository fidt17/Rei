#pragma once

#include <memory>
#include <vector>

#define GLFW_EXPOSE_NATIVE_WIN32
#include "Ecs/ComponentRef.h"
#include "CustomRenderModule.h"
#include "RenderScenario/BaseRenderScenario.h"

namespace rei::render
{
    class Renderer
    {
    public:
        explicit Renderer(std::vector<std::unique_ptr<CustomRenderModule>> customRenderModules = {});

        void SetCamera(const ecs::ComponentRef<Camera>& camera);
        ecs::ComponentRef<Camera> GetCamera() const;

        void SetTarget(GLFWwindow* target);

        void Render() const;
        bool RequestFrameCapture(const FrameCaptureCallback& callback) const;

        void Dispose();

    private:
        GLFWwindow* _target = nullptr;
        ecs::ComponentRef<Camera> _camera;
        std::vector<std::unique_ptr<CustomRenderModule>> _customRenderModules;
        std::unique_ptr<BaseRenderScenario> _renderScenario = nullptr;

        void PrepareAssets() const;
    };
}
