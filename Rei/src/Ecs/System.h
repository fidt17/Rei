#pragma once
#include "World.h"

namespace rei::ecs
{
    class System
    {
        // Keep existing pointer/conversion access while avoiding World ownership.
        class WeakWorldRef
        {
        public:
            explicit WeakWorldRef(const std::shared_ptr<World>& world) : _world(world) {}
            std::shared_ptr<World> operator->() const { return Lock(); }
            operator std::shared_ptr<World>() const { return Lock(); }

        private:
            std::weak_ptr<World> _world;

            std::shared_ptr<World> Lock() const
            {
                auto world = _world.lock();
                REI_THROW_IF(world == nullptr, "System World has been destroyed")
                return world;
            }
        };

    public:
        System(const std::shared_ptr<World>& ecsWorld)
            : _ecsWorld(ecsWorld),
              _ecs(ecsWorld->GetRegistry())
        {
        }

        virtual ~System() = default;

        virtual void OnUpdate() = 0;

    protected:
        const WeakWorldRef _ecsWorld;
        const std::shared_ptr<EcsRegistry> _ecs;
    };
}
