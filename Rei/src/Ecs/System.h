#pragma once
#include "World.h"

namespace rei::ecs
{
    class System
    {
    public:
        System(const std::shared_ptr<World>& ecsWorld)
            : _ecsWorld(ecsWorld),
              _ecs(ecsWorld->GetRegistry())
        {
        }

        virtual ~System() = default;

        virtual void OnUpdate() = 0;

    protected:
        const std::shared_ptr<World> _ecsWorld;
        const std::shared_ptr<EcsRegistry> _ecs;
    };
}
