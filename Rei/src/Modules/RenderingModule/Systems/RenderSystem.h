#pragma once
#include "Modules/RenderingModule/Renderer.h"

namespace rei::render
{
    class RenderSystem final : public ecs::System
    {
    public:
        RenderSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters, const std::shared_ptr<Renderer>& renderer);

        void OnUpdate() override;

    private:
        std::shared_ptr<Renderer> _renderer;
    };
}
