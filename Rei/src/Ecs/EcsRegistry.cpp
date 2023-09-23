#include "pch.h"
#include "EcsRegistry.h"

namespace rei::ecs
{
    Entity EcsRegistry::NewEntity()
    {
        if (_deadEntitiesPool.empty())
        {
            return AllocateNewEntity();
        }
        return GetFromPool();
    }

    BitMask& EcsRegistry::GetEntityMask(const Entity e)
    {
        REI_THROW_IF(IsDead(e), "Cannot get mask of dead entity");
            
        return _entityMasks[e.Id];
    }

    bool EcsRegistry::IsAlive(const Entity e) const
    {
        return _entities[e.Id].Generation == e.Generation;
    }

    bool EcsRegistry::IsDead(const Entity e) const
    {
        return !IsAlive(e);
    }

    void EcsRegistry::DestroyEntity(const Entity e)
    {
        GetEntityMask(e).Clear();
        _dirtyEntities.insert(e);
        _destroyedEntities.insert(e);
    }

    void EcsRegistry::HandleDeadEntity(const Entity e)
    {
        _deadEntitiesPool.push(e);
        _entities[e.Id].Generation = 0;
    }

    Entity EcsRegistry::GetEntityById(const EntityId id) const
    {
        return _entities[id];
    }

    const std::unordered_set<Entity>& EcsRegistry::GetDirtyEntities() const
    {
        return _dirtyEntities;
    }

    const std::unordered_set<Entity>& EcsRegistry::GetDestroyedEntities() const
    {
        return _destroyedEntities;
    }

    const std::vector<std::shared_ptr<IComponentSet>>& EcsRegistry::GetComponentSets() const
    {
        return _componentSets;
    }

    void EcsRegistry::ClearDirtyEntities()
    {
        _dirtyEntities.clear();
    }

    void EcsRegistry::ClearDestroyedEntities()
    {
        _destroyedEntities.clear();
    }

    void EcsRegistry::ResizeMasks(const size_t size)
    {
        for (auto& entityMask : _entityMasks)
        {
            entityMask.Resize(size);
        }
    }

    Entity EcsRegistry::AllocateNewEntity()
    {
        const auto id = _entities.size();

        _entities.emplace_back(static_cast<EntityId>(id), 1);
        _entityMasks.emplace_back().Resize(_maxComponentId);
        return _entities.back();
    }
    
    Entity EcsRegistry::GetFromPool()
    {
        const auto id = _deadEntitiesPool.front().Id;
        const auto gen = _deadEntitiesPool.front().Generation;
        _deadEntitiesPool.pop();

        _entities[id].Generation = gen + 1;
        return _entities[id];
    }
}
