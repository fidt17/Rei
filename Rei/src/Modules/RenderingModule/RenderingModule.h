#pragma once
#include "Renderer.h"

namespace rei::render
{
    class RenderingModule : public ecs::IEcsModule
    {
    public:
        explicit RenderingModule(const std::shared_ptr<Renderer>& renderer);
        void Configure(std::shared_ptr<ecs::World>) override;

    private:
        std::shared_ptr<Renderer> _renderer;
    };
}
