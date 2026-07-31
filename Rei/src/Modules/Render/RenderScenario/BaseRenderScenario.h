#pragma once
#include <functional>
#include "../resources/rei_behaviours/render/camera/Camera.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"

namespace rei::render
{
    using FrameCaptureCallback = std::function<void(const u8*, i32, i32)>;

    class BaseRenderScenario
    {
    public:
        explicit BaseRenderScenario(GLFWwindow* target);
        virtual ~BaseRenderScenario() = default;

        virtual void SetCamera(const ecs::ComponentRef<Camera>& camera);
        virtual void Setup() = 0;
        virtual void OnBeforeRender() = 0;
        virtual void Render() = 0;
        virtual void RenderWithoutCamera() = 0;
        virtual bool RequestFrameCapture(const FrameCaptureCallback& callback) = 0;
        virtual void Dispose() = 0;

        void Clear() const;

        bool IsCameraSet() const;

    protected:
        GLFWwindow* _target;
        ecs::ComponentRef<Camera> _camera;
    };
}
