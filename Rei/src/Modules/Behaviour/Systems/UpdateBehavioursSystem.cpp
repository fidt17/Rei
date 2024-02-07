#include "pch.h"
#include "UpdateBehavioursSystem.h"

#include "Engine/Services.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/EntityManagement/EntityManager.h"

UpdateBehavioursSystem::UpdateBehavioursSystem(const std::shared_ptr<rei::ecs::EcsRegistry>& ecs,
                                               const std::shared_ptr<rei::ecs::FilterProvider>& filters): System(ecs, filters)
{
    _f = filters->Get<BehaviourCollection>();
}

void UpdateBehavioursSystem::OnUpdate()
{
    const auto& entityManager = rei::GetEntityManager();
    FOR(e, _f)
    {
        for (const auto behavioursToInit : GET(e, BehaviourCollection).Behaviours)
        {
            entityManager.GetComponent(e, behavioursToInit).Update();
        }
    }
}
