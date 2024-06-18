#include "pch.h"
#include "AssignMainCameraSystem.h"

rei::render::AssignMainCameraSystem::AssignMainCameraSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters,
    const std::shared_ptr<Renderer>& renderer):
    System(ecs, filters),
    _f(filters->Get<Camera>()),
    _renderer(renderer)
{ }

void rei::render::AssignMainCameraSystem::OnUpdate()
{
    if (!_renderer->GetCamera().IsNull()) return;

    FOR(e, _f)
    {
        _renderer->SetCamera(GET_REF(e, Camera));
        return;
    }
}
