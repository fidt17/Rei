#include "pch.h"
#include "UpdateBehavioursSystem.h"

#include "Engine/Services.h"
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
            for (const auto behavioursToInit : GET(e, BehaviourCollection).Behaviours)
            {
                _entityManager->GetComponent(e, behavioursToInit).Update();
            }
        }
    }
}
