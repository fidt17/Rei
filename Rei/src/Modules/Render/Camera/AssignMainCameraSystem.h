#pragma once
#include "Ecs/System.h"
#include "Modules/Render/Renderer.h"

namespace rei::render
{
    class Camera;

    class AssignMainCameraSystem : public ecs::System
    {
    public:
        AssignMainCameraSystem(
            const std::shared_ptr<ecs::World>& world,
            const std::shared_ptr<Renderer>& renderer);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _cameraFilter;
        std::shared_ptr<Renderer> _renderer;
    };
}
