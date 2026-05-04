#pragma once

#include "Ecs/System.h"

namespace rei::editor
{
    class UIPointerCollisionSystem : public ecs::System
    {
    public:
        explicit UIPointerCollisionSystem(const std::shared_ptr<ecs::World>& world);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _entities;
    };
}
