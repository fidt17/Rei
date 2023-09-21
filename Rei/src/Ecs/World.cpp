#include "pch.h"
#include "World.h"
#include "Entity.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    World::World(): _ecsRegistry(std::make_shared<EcsRegistry>()),
                    _filterRegistry(std::make_shared<FiltersRegistry>())
    {
        _ecsRegistry->MaxComponentIdChangedEvent += std::make_shared<std::function<void(u32)>>([this](const u32 s) { UpdateBitMasks(s); });
    }

    void World::Run()
    {
        for (const auto& system : _systems)
        {
            system->OnUpdate();
            Refresh();
        }
    }

    void World::Refresh()
    {
        auto& dirtyEntities = _ecsRegistry->GetDirtyEntities();
        for (const auto& e : dirtyEntities)
        {
            _filterRegistry->HandleEntityChange(e, _ecsRegistry->GetEntityMask(e));
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
