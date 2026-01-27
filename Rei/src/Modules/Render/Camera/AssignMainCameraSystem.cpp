#include "pch.h"
#include "AssignMainCameraSystem.h"

#include "MainCameraTag.h"

namespace rei::render
{
    AssignMainCameraSystem::AssignMainCameraSystem(const std::shared_ptr<ecs::World>& world, const std::shared_ptr<Renderer>& renderer):
        System(world),
        _renderer(renderer)
    {
        _cameraFilter = FILTER(Camera);
    }

    void AssignMainCameraSystem::OnUpdate()
    {
        if (!_renderer->GetCamera().IsNull()) return;

        FOR(e, _cameraFilter)
        {
            _renderer->SetCamera(GET_REF(e, Camera));
            GET(e, MainCameraTag);
            return;
        }
    }
}
