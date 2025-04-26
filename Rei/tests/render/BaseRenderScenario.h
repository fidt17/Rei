#pragma once

class BaseRenderScenario
{
public:
    explicit BaseRenderScenario(GLFWwindow* target)
        : _target(target)
    {
    }

    void SetCamera(const rei::ecs::RefComponent<rei::render::Camera>& camera) { _camera = camera; }
    virtual void Setup() = 0;
    virtual void Render() = 0;
    virtual void Dispose() = 0;

    void Clear() const
    {
        glClearColor(0, 0, 0, 1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        glfwSwapBuffers(_target);
    }

    bool IsCameraSet() const { return !_camera.IsNull(); }

protected:
    GLFWwindow* _target;
    rei::ecs::RefComponent<rei::render::Camera> _camera;
};
