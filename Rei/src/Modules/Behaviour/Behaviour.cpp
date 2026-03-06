#include "pch.h"
#include "Behaviour.h"

#include "Engine/Services.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei
{
    Behaviour::Behaviour(const i32 id, const ecs::Entity e):
        _id(id),
        _entity(e),
        _transform(rei::ecs::RefComponent<Transform>(GetInternalWorld()->GetRegistry(), e))
    {
    }

    i32 Behaviour::GetBehaviourId() const
    {
        return _id;
    }

    ecs::Entity Behaviour::GetEntity() const
    {
        return _entity;
    }

    Transform& Behaviour::GetTransform() const
    {
        return _transform;
    }

    bool Behaviour::IsEnabled() const
    {
        return _enabled;
    }

    void Behaviour::Enable()
    {
        _enabled = true;
    }

    void Behaviour::Disable()
    {
        _enabled = false;
    }
}
