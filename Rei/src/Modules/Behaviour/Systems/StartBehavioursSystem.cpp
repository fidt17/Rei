#include "pch.h"
#include "StartBehavioursSystem.h"

#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::behaviour
{
    StartBehavioursSystem::StartBehavioursSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs,
                                                 const std::shared_ptr<ecs::FilterProvider>& filters,
                                                 const std::shared_ptr<EntityManager>& entityManager) :
        System(ecs, filters),
        _entityManager(entityManager)
    {
        _f = filters->Get<StartBehavioursEvent>();
    }

    void StartBehavioursSystem::OnUpdate()
    {
        FOR(e, _f)
        {
            const auto behaviours = GET(e, StartBehavioursEvent).Behaviours; // here we make a copy for cases when new behaviours would be added during start loop
            for (const auto behaviourToStart : behaviours)
            {
                _entityManager->GetBehaviour(e, behaviourToStart).Start();
            }
        }
    }
}
