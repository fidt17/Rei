#include "pch.h"
#include "World.h"
#include "Entity.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    Entity World::NewEntity()
    {
        if (_lastId == ENTITIES_PER_GENERATION)
        {
            _lastId = 0;
            _currentGeneration += 1;
        }

        const auto id = _lastId++;
        const auto gen = _currentGeneration;
        return _ecsRegistry->NewEntity(id, gen);
    }

    void World::Refresh() const
    {
        auto& dirtyEntities = _ecsRegistry->GetDirtyEntities();
        auto& filters = _filterRegistry->GetFilters();
        for (const auto& changedEntity : dirtyEntities)
        {
            const auto& entityMask = _ecsRegistry->GetEntityMask(changedEntity);
            for (const auto& filter : filters)
            {
                filter->OnEntityChange(changedEntity, entityMask);
            }
        }
        _ecsRegistry->ClearDirtyEntities();
    }

    void World::UpdateBitMasks(const u32 size) const
    {
        if (size < sizeof(BitMask::mask) * 8) return;

        _ecsRegistry->ResizeMasks(size);
        _filterRegistry->ResizeMasks(size);
    }
}
