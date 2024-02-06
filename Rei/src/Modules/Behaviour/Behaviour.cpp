#include "pch.h"
#include "Behaviour.h"

i32 rei::Behaviour::GetId() const
{
    return _id;
}

rei::ecs::Entity rei::Behaviour::GetEntity() const
{
    return _entity;
}

