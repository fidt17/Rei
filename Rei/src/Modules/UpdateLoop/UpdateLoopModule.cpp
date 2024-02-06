#include "pch.h"
#include "UpdateLoopModule.h"

#include "Ecs/DeleteHere.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Behaviour/Systems/StartBehavioursSystem.h"
#include "Modules/Behaviour/Systems/UpdateBehavioursSystem.h"
#include "Systems/InvokeUpdateCallbackSystem.h"

namespace rei::internal::update_loop
{
    void UpdateLoopModule::Configure(const std::shared_ptr<ecs::World> w)
    {
        w->AddSystem<StartBehavioursSystem>();
        w->AddSystem<DeleteHere<StartBehavioursEvent>>();
        
        w->AddSystem<InvokeUpdateCallbackSystem>();
        w->AddSystem<UpdateBehavioursSystem>();
    }
}
