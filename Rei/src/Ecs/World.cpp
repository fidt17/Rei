#include "pch.h"
#include "World.h"

#include "Entity.h"
#include "FiltersRegistry.h"
#include "Systems/CallbackSystem.h"

namespace rei::ecs
{
    World::World(): _ecsRegistry(std::make_shared<EcsRegistry>()),
                    _filterRegistry(std::make_shared<FiltersRegistry>())
    {
        _componentIdChangedHandle = _ecsRegistry->MaxComponentIdChangedEvent.append([this](const size_t s) { UpdateBitMasks(s); });
        _newFilterCreatedHandle = _filterRegistry->NewFilterCreatedEvent.append([this]
        {
            UpdateBitMasks(_filterRegistry->GetMaxMaskBitIndex());
            RefreshAll();
        });
    }

    World::~World()
    {
        _ecsRegistry->MaxComponentIdChangedEvent.remove(_componentIdChangedHandle);
        _filterRegistry->NewFilterCreatedEvent.remove(_newFilterCreatedHandle);
    }

    void World::AddSystem(const std::function<void()>& fn)
    {
        AddSystem<CallbackSystem>(fn);
    }

    void World::Run() const
    {
        Refresh();

        for (const auto& system : _systems)
        {
            system->OnUpdate();
            Refresh();
        }
    }

    void World::Refresh() const
    {
        const auto& destroyedEntities = _ecsRegistry->GetDestroyedEntities();
        auto& dirtyEntities = _ecsRegistry->GetDirtyEntities();
        for (const auto& e : dirtyEntities)
        {
            _filterRegistry->HandleEntityChange(e, _ecsRegistry->GetEntityMask(e), destroyedEntities.count(e) == 0);
        }
        _ecsRegistry->ClearDirtyEntities();

        auto& sets = _ecsRegistry->GetComponentSets();
        for (auto e : destroyedEntities)
        {
            for (auto& set : sets)
            {
                if (!set) continue;
                set->Delete(e);
            }
            _ecsRegistry->HandleDeadEntity(e);
        }
        _ecsRegistry->ClearDestroyedEntities();
    }

    void World::RefreshAll() const
    {
        const auto& destroyedEntities = _ecsRegistry->GetDestroyedEntities();
        for (const auto& e : _ecsRegistry->GetAllEntities())
        {
            if (_ecsRegistry->IsDead(e)) continue;

            const auto mask = _ecsRegistry->GetEntityMask(e);
            _filterRegistry->HandleEntityChange(e, mask, destroyedEntities.count(e) == 0);
        }
    }

    std::shared_ptr<EcsRegistry> World::GetRegistry()
    {
        return _ecsRegistry;
    }

    std::shared_ptr<FiltersRegistry> World::GetFiltersRegistry()
    {
        return _filterRegistry;
    }

    void World::UpdateBitMasks(const size_t size) const
    {
        if (size < sizeof(BitMask::mask) * 8) return;

        _ecsRegistry->ResizeMasks(size);
        _filterRegistry->ResizeMasks(size);
    }
}
