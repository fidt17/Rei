#include "pch.h"
#include "World.h"
#include "Entity.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    Entity& World::NewEntity()
    {
        if (_lastId == ENTITIES_PER_GENERATION)
        {
            _lastId = 0;
            _currentGeneration += 1;
        }

        const auto id = _lastId++;
        const auto gen = _currentGeneration;

        _entities.emplace_back(id, gen);

        return _entities.back();
    }

    void World::Refresh() const
    {
        auto& dirtyEntities = _ecsRegistry->GetDirtyEntities();
        auto& filters = _filterRegistry->GetFilters();
        for (const auto& changedEntity : dirtyEntities)
        {
            for (const auto& filter : filters)
            {
                filter->OnEntityChange(changedEntity);
            }
        }
        _ecsRegistry->ClearDirtyEntities();
    }
}
