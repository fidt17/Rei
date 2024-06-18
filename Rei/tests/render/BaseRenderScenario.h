#pragma once

class BaseRenderScenario
{
public:
    explicit BaseRenderScenario(GLFWwindow* target)
        : _target(target), _camera()
    {
    }

    void SetCamera(const rei::ecs::RefComponent<rei::render::Camera>& camera) { _camera = camera; }
    virtual void Setup() = 0;
    virtual void Render() = 0;
    virtual void Dispose() = 0;

protected:
    GLFWwindow* _target;
    rei::ecs::RefComponent<rei::render::Camera> _camera;
};