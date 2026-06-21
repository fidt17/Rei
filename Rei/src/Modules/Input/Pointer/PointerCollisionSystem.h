#pragma once
#include "Ecs/System.h"

namespace rei::input
{
    class PointerCollisionSystem : public ecs::System
    {
    public:
        PointerCollisionSystem(const std::shared_ptr<ecs::World>& world);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _entities;
    };
}
