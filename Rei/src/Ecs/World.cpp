#include "pch.h"
#include "World.h"
#include "Entity.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    void World::Refresh()
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

        auto& destroyedEntities = _ecsRegistry->GetDestroyedEntities();
        auto& sets = _ecsRegistry->GetComponentSets();
        for (auto e : destroyedEntities)
        {
            for (auto& set : sets)
            {
                set->Delete(e);
            }
            _ecsRegistry->HandleDeadEntity(e);
        }
        _ecsRegistry->ClearDestroyedEntities();
    }

    std::shared_ptr<EcsRegistry> World::GetRegistry()
    {
        return _ecsRegistry;
    }

    std::shared_ptr<FiltersRegistry> World::GetFiltersRegistry()
    {
        return _filterRegistry;
    }

    void World::UpdateBitMasks(const u32 size) const
    {
        if (size < sizeof(BitMask::mask) * 8) return;

        _ecsRegistry->ResizeMasks(size);
        _filterRegistry->ResizeMasks(size);
    }
}
