#include "pch.h"
#include "StartBehavioursSystem.h"

#include "Engine/Services.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/EntityManagement/EntityManager.h"

StartBehavioursSystem::StartBehavioursSystem(const std::shared_ptr<rei::ecs::EcsRegistry>& ecs, const std::shared_ptr<rei::ecs::FilterProvider>& filters): System(ecs, filters)
{
    _f = filters->Get<StartBehavioursEvent>();
}

void StartBehavioursSystem::OnUpdate()
{
    const auto& entityManager = rei::GetEntityManager();
    FOR(e, _f)
    {
        for (const auto behavioursToInit : GET(e, StartBehavioursEvent).Behaviours)
        {
            entityManager.GetComponent(e, behavioursToInit).Start();
        }
    }
}
