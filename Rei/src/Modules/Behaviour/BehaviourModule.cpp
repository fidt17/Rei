#include "pch.h"
#include "BehaviourModule.h"

#include "Components/StartBehavioursCommand.h"
#include "Ecs/DeleteSystem.h"
#include "Systems/StartBehavioursSystem.h"

void BehaviourModule::Configure(const std::shared_ptr<rei::ecs::World> w)
{
    w->AddSystem<StartBehavioursSystem>();
    w->AddSystem<DeleteSystem<StartBehavioursCommand>>();
}
