#include "pch.h"
#include "RenderSystem.h"

namespace rei::render
{
    RenderSystem::RenderSystem(
        const std::shared_ptr<ecs::EcsRegistry>& ecs,
        const std::shared_ptr<ecs::FilterProvider>& filters,
        const std::shared_ptr<Renderer>& renderer) :
        System(ecs, filters),
        _renderer(renderer)
    {
    }

    void RenderSystem::OnUpdate()
    {
        _renderer->Render();
    }
}
