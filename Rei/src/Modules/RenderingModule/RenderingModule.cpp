#include "pch.h"
#include "RenderingModule.h"

#include "Systems/RenderSystem.h"

namespace rei::render
{
    RenderingModule::RenderingModule(const std::shared_ptr<Renderer>& renderer) :
        _renderer(renderer)
    {
    }

    void RenderingModule::Configure(std::shared_ptr<ecs::World> w)
    {
        w->AddSystem<RenderSystem>(_renderer);
    }
}
