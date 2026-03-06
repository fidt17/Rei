#include "pch.h"
#include "CallbackSystem.h"

namespace rei::ecs
{
    CallbackSystem::CallbackSystem(const std::shared_ptr<World>& world, const std::function<void()>& callback)
        : System(world),
          _callback(callback)
    {
    }

    void CallbackSystem::OnUpdate()
    {
        _callback();
    }
}
