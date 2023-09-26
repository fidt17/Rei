#include "pch.h"
#include "UpdateLoopModule.h"

#include "Systems/InvokeUpdateCallbackSystem.h"

namespace rei::internal::update_loop
{
    void UpdateLoopModule::Configure(const std::shared_ptr<ecs::World> world)
    {
        world->AddSystem<InvokeUpdateCallbackSystem>();
    }
}
