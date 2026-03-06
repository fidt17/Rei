#include "pch.h"
#include "BaseRenderScenario.h"

rei::render::BaseRenderScenario::BaseRenderScenario(GLFWwindow* target)
: _target(target)
{
}

void rei::render::BaseRenderScenario::SetCamera(const ecs::RefComponent<Camera>& camera)
{
    _camera = camera;
}

void rei::render::BaseRenderScenario::Clear() const
{
    glClearColor(0, 0, 0, 1);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
    glfwSwapBuffers(_target);
}

bool rei::render::BaseRenderScenario::IsCameraSet() const
{
    return !_camera.IsNull();
}
