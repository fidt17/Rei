#pragma once
#include "../resources/rei_behaviours/render/camera/Camera.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"

namespace rei::render
{
    class BaseRenderScenario
    {
    public:
        explicit BaseRenderScenario(GLFWwindow* target);
        virtual ~BaseRenderScenario() = default;

        virtual void SetCamera(const ecs::ComponentRef<Camera>& camera);
        virtual void Setup() = 0;
        virtual void OnBeforeRender() = 0;
        virtual void Render() = 0;
        virtual void Dispose() = 0;

        void Clear() const;

        bool IsCameraSet() const;

    protected:
        GLFWwindow* _target;
        ecs::ComponentRef<Camera> _camera;
    };
}
