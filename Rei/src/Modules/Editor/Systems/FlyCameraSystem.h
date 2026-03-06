#pragma once
#include "Ecs/System.h"

namespace rei::editor
{
    class FlyCameraSystem : public ecs::System
    {
    public:
        FlyCameraSystem(const std::shared_ptr<ecs::World>& world);

        void MoveCamera(Transform& transform, f32 cameraSpeed) const;
        void RotateCamera(Transform& transform) const;
        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _cameraFilter;
    };
}
