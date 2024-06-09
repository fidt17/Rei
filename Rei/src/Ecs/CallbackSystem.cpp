#include "pch.h"
#include "CallbackSystem.h"

rei::ecs::CallbackSystem::CallbackSystem(const std::shared_ptr<EcsRegistry>& ecs, const std::shared_ptr<FilterProvider>& filters,
    const std::function<void()>& callback): System(ecs, filters),
                                            _callback(callback)
{
}

void rei::ecs::CallbackSystem::OnUpdate()
{
    _callback();
}
