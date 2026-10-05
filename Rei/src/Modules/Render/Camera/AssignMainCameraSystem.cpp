#include "pch.h"
#include "AssignMainCameraSystem.h"

#include "MainCameraTag.h"

namespace rei::render
{
    AssignMainCameraSystem::AssignMainCameraSystem(const std::shared_ptr<ecs::World>& world, const std::shared_ptr<Renderer>& renderer):
        System(world),
        _renderer(renderer)
    {
    }

    void AssignMainCameraSystem::OnUpdate()
    {
        const auto next = Camera::GetMainCamera();
        const auto entity = next.IsNull() ? ecs::NULL_ENTITY : next.Get().GetEntity();
        const auto current = _renderer->GetCamera();
        const bool matchesRenderer = next.IsNull() ? current.IsNull() : !current.IsNull() && current.Get().GetEntity() == entity;
        if (_taggedCamera == entity && matchesRenderer) return;

        if (_taggedCamera != ecs::NULL_ENTITY && !_ecs->IsDead(_taggedCamera)) _ecs->Del<MainCameraTag>(_taggedCamera);
        _renderer->SetCamera(next);
        _taggedCamera = entity;
        if (!next.IsNull()) _ecs->Get<MainCameraTag>(entity);
    }
}
