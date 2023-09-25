#include "pch.h"
#include "InvokeUpdateCallbackSystem.h"

#include "Modules/UpdateLoop/Components/UpdateCallback.h"

namespace rei::internal::update_loop
{
    InvokeUpdateCallbackSystem::InvokeUpdateCallbackSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters)
        : System(ecs, filters),
          _f(filters->Get<UpdateCallback>())
    {
    }

    void InvokeUpdateCallbackSystem::OnUpdate()
    {
        FOR(_f)
        {
            GET(e, UpdateCallback).Callback();
        }
    }
}
