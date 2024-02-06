#include "pch.h"
#include "StartBehavioursSystem.h"

#include "Engine/Services.h"
#include "Modules/Behaviour/Components/StartBehavioursCommand.h"
#include "Modules/EntityManagement/EntityManager.h"

StartBehavioursSystem::StartBehavioursSystem(const std::shared_ptr<rei::ecs::EcsRegistry>& ecs, const std::shared_ptr<rei::ecs::FilterProvider>& filters): System(ecs, filters)
{
    _f = filters->Get<StartBehavioursCommand>();
}

void StartBehavioursSystem::OnUpdate()
{
    FOR(e, _f)
    {
        for (const auto behavioursToInit : GET(e, StartBehavioursCommand).Behaviours)
        {
            rei::GetEntityManager().GetBehaviour(e, behavioursToInit).Start();
        }
    }
}
