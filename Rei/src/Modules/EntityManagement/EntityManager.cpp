#include "pch.h"
#include "EntityManager.h"

#include "Modules/Components/EntityInfo.h"

rei::EntityManager::EntityManager(const std::shared_ptr<ecs::World>& world)
    : _internalWorld(world),
      _entityInfoFilter(world->GetFiltersRegistry()->Get<EntityInfo>())
{
}

rei::ecs::Entity rei::EntityManager::GetBySceneId(const i32 id) const
{
    for (const auto e : *_entityInfoFilter)
    {
        if (_internalWorld->GetRegistry()->Get<EntityInfo>(e).Id == id)
        {
            return e;
        }
    }
    
    return ecs::NULL_ENTITY;
}
