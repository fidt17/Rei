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
            for (const auto behavioursToInit : GET(e, StartBehavioursEvent).Behaviours)
            {
                _entityManager->GetComponent(e, behavioursToInit).Start();
            }
        }
    }
}
