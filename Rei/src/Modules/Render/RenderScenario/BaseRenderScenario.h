#pragma once
#include "../resources/rei_behaviours/render/Camera.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"

namespace rei::render
{
    class BaseRenderScenario
    {
    public:
        explicit BaseRenderScenario(GLFWwindow* target);
        virtual ~BaseRenderScenario() = default;

        void SetCamera(const ecs::RefComponent<Camera>& camera);
        virtual void Setup() = 0;
        virtual void OnBeforeRender() = 0;
        virtual void Render() = 0;
        virtual void Dispose() = 0;

        void Clear() const;

        bool IsCameraSet() const;

    protected:
        GLFWwindow* _target;
        ecs::RefComponent<Camera> _camera;
    };
}
