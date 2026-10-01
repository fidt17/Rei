#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Common/Time/Stopwatch.h"
#include "Common/Diagnostics/DiagnosticsService.h"
#include "Engine/Services.h"
#include "BaseRenderScenario.h"

rei::render::BaseRenderScenario::BaseRenderScenario(GLFWwindow* target)
: _target(target)
{
}

void rei::render::BaseRenderScenario::SetCamera(const ecs::ComponentRef<Camera>& camera)
{
    _camera = camera;
}

void rei::render::BaseRenderScenario::Clear() const
{
    glClearColor(0, 0, 0, 1);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
    time::Stopwatch stopwatch;
    stopwatch.Start();
    {
        REI_PROFILE_SCOPE(profiling::markers::SWAP.Id);
        glfwSwapBuffers(_target);
    }
    stopwatch.Stop();
    GetDiagnostics().SetPresentTime(stopwatch.ElapsedMs());
}

bool rei::render::BaseRenderScenario::IsCameraSet() const
{
    return !_camera.IsNull();
}
