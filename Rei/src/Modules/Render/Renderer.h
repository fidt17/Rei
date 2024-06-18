#pragma once

#define GLFW_EXPOSE_NATIVE_WIN32
#include "../../../resources/rei_behaviours/render/Camera.h"
#include "Ecs/RefComponent.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "glfw/glfw3native.h"

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
    };
}
