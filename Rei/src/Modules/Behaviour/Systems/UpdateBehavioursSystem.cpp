#include "pch.h"
#include "UpdateBehavioursSystem.h"

#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::behaviour
{
    UpdateBehavioursSystem::UpdateBehavioursSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs,
                                                   const std::shared_ptr<ecs::FilterProvider>& filters,
                                                   const std::shared_ptr<EntityManager>& entityManager) :
        System(ecs, filters),
        _entityManager(entityManager)
    {
        _f = filters->Get<BehaviourCollection>();
    }

    void UpdateBehavioursSystem::OnUpdate()
    {
        FOR(e, _f)
        {
            // here we make a copy for cases when new behaviours would be added during update loop
            const auto behaviours = GET(e, BehaviourCollection).Behaviours;
            for (const auto behaviourId : behaviours)
            {
                auto& behaviour = _entityManager->GetBehaviour(e, behaviourId);
                if (behaviour.IsEnabled())
                {
                    behaviour.Update();
                }
            }
        }
    }
}
