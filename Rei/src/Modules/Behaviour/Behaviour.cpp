#include "pch.h"
#include "Behaviour.h"

#include "Engine/Services.h"
#include "Modules/Components/EntityInfo.h"

void rei::Behaviour::Construct(const ecs::Entity e, const EntityInfo& entityInfo)
{
    _entity = e;
    _sceneId = entityInfo.Id;
}

rei::ecs::Entity rei::Behaviour::GetEntity() const
{
    return _entity;
}

const std::string& rei::Behaviour::GetName() const
{
    ECS_WORLD(GetInternalWorld());
    return GET(_entity, EntityInfo).Name;
}

i32 rei::Behaviour::GetSceneId() const
{
    return _sceneId;
}
