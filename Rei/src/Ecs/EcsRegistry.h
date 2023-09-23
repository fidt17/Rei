#pragma once
#include <queue>
#include <unordered_set>

#include "BitMask.h"
#include "ComponentSet.h"
#include "TypeId.h"

namespace rei::ecs
{
    class World;

    class EcsRegistry
    {
    public:
        REI_EVENT(size_t) MaxComponentIdChangedEvent;

        REI_API Entity NewEntity();

        REI_API BitMask& GetEntityMask(Entity e);

        template <typename T>
        REI_API T& Get(Entity e)
        {
            REI_THROW_IF(IsDead(e), "Cannot get component from dead entity");
            
            auto componentSet = GetSet<T>();

            bool didAddComponent = false;
            auto& component = componentSet->Get(e, didAddComponent);

            if (didAddComponent)
            {
                const u64 componentSetId = componentSet->Id();
                GetEntityMask(e).Set(componentSetId);
                _dirtyEntities.insert(e);
            }

            return component;
        }

        template <typename T>
        REI_API bool Has(const Entity e)
        {
            REI_THROW_IF(IsDead(e), "Cannot check if dead entity has component")
            
            auto componentSet = GetSet<T>();
            return componentSet->Has(e);
        }

        template <typename T>
        REI_API void Del(Entity e)
        {
            REI_THROW_IF(IsDead(e), "Cannot delete component on dead entity")
        
            auto set = GetSet<T>();
            if (set->Delete(e))
            {
                GetEntityMask(e).Remove(set->Id());
                _dirtyEntities.insert(e);
            }
        }

        REI_API bool IsAlive(Entity e) const;

        REI_API bool IsDead(Entity e) const;

        REI_API void DestroyEntity(Entity e);

        void HandleDeadEntity(Entity e);

        REI_API Entity GetEntityById(EntityId id) const;

        const std::unordered_set<Entity>& GetDirtyEntities() const;
        void ClearDirtyEntities();
        
        const std::unordered_set<Entity>& GetDestroyedEntities() const;
        void ClearDestroyedEntities();
        
        const std::vector<std::shared_ptr<IComponentSet>>& GetComponentSets() const;

        void ResizeMasks(size_t size);

    private:
        std::vector<std::shared_ptr<IComponentSet>> _componentSets{};
        std::unordered_set<Entity> _dirtyEntities{};
        std::unordered_set<Entity> _destroyedEntities{};
        std::queue<Entity> _deadEntitiesPool{};

        std::vector<Entity> _entities{};
        std::vector<BitMask> _entityMasks{};

        size_t _maxComponentId = 0;
        EntityGen _currentGeneration = 1;

        template <typename T>
        std::shared_ptr<ComponentSet<T>> GetSet()
        {
            const auto componentId = TypeId::Get<T>();
            for (auto i = _componentSets.size(); i <= componentId; i++)
            {
                CreateComponentSet<T>(i);
            }

            return std::static_pointer_cast<ComponentSet<T>>(_componentSets.at(componentId));
        }
        
        template <typename T>
        std::shared_ptr<ComponentSet<T>> CreateComponentSet(size_t id)
        {
            auto set = std::make_shared<ComponentSet<T>>(id);
            _componentSets.push_back(std::static_pointer_cast<IComponentSet>(set));

            if (_maxComponentId < set->Id())
            {
                _maxComponentId = set->Id();
                MaxComponentIdChangedEvent.Invoke(_maxComponentId);
            }

            return set;
        }

        Entity AllocateNewEntity();
        Entity GetFromPool();
    };
}
