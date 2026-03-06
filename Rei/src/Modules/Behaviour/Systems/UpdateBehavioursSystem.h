#pragma once
#include "Ecs/System.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::behaviour
{
    class UpdateBehavioursSystem final : public ecs::System
    {
    public:
        UpdateBehavioursSystem(
            const std::shared_ptr<ecs::World>&,
            const std::shared_ptr<EntityManager>&);

        void OnUpdate() override;
        
    private:
        std::shared_ptr<ecs::Filter> _f;
        std::shared_ptr<EntityManager> _entityManager;
    };
}
