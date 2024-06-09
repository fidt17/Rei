#pragma once

namespace rei
{
    class EntityManager;
}

namespace rei::behaviour
{
    class StartBehavioursSystem final : public ecs::System
    {
    public:
        StartBehavioursSystem(const std::shared_ptr<ecs::EcsRegistry>&, const std::shared_ptr<ecs::FilterProvider>&, const std::shared_ptr<EntityManager>&);

        void OnUpdate() override;
        
    private:
        std::shared_ptr<ecs::Filter> _f;
        std::shared_ptr<EntityManager> _entityManager;
    };
}
