#include "pch.h"
#include "Behaviour.h"

#include "Engine/Services.h"
#include "rei_behaviours/transformation/Transform.h"

rei::Behaviour::Behaviour(const i32 id, const ecs::Entity e):
    _id(id),
    _entity(e),
    _transform(rei::ecs::RefComponent<transformation::Transform>(GetInternalWorld().GetRegistry(), e))
{
}

i32 rei::Behaviour::GetBehaviourId() const
{
    return _id;
}

rei::ecs::Entity rei::Behaviour::GetEntity() const
{
    return _entity;
}

rei::transformation::Transform& rei::Behaviour::GetTransform() const
{
    return _transform;
}

