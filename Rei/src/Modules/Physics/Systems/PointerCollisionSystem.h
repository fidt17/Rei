#pragma once

namespace rei::physics
{
    class PointerCollisionSystem : public ecs::System
    {
    public:
        PointerCollisionSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _entities;
    };
}
