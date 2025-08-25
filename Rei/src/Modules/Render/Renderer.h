#pragma once

#define GLFW_EXPOSE_NATIVE_WIN32
#include "Ecs/RefComponent.h"
#include "RenderScenario/BaseRenderScenario.h"

namespace rei::render
{
    class Renderer
    {
    public:
        Renderer() = default;

        void SetCamera(const ecs::RefComponent<Camera>& camera);
        ecs::RefComponent<Camera> GetCamera() const;

        void SetTarget(GLFWwindow* target);

        void Render() const;

        void Dispose();

    private:
        GLFWwindow* _target = nullptr;
        ecs::RefComponent<Camera> _camera;
        std::unique_ptr<BaseRenderScenario> _renderScenario = nullptr;

        void PrepareMaterials() const;
    };
}
