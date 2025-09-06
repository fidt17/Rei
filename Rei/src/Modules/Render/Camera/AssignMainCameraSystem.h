#pragma once
#include "Modules/Render/Renderer.h"

namespace rei::render
{
    class Camera;

    class AssignMainCameraSystem : public ecs::System
    {
    public:
        AssignMainCameraSystem(
            const std::shared_ptr<ecs::EcsRegistry>& ecs,
            const std::shared_ptr<ecs::FilterProvider>& filters,
            const std::shared_ptr<Renderer>& renderer);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _f;
        std::shared_ptr<Renderer> _renderer;
    };
}
