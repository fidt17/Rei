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
        REI_EVENT(u32) MaxComponentIdChangedEvent;

        Entity NewEntity()
        {
            if (_deadEntities.empty())
            {
                const auto id = _entities.size();
                const auto gen = 1;
                
                _entities.emplace_back(id, gen);
                _entityMasks.emplace_back().Resize(_maxComponentId);
                return _entities.back();
            }

            auto id = _deadEntities.front().Id;
            auto gen = _deadEntities.front().Generation;
            _deadEntities.pop();
            
            _entities[id].Generation = gen + 1;
            return _entities[id];
        }

        BitMask& GetEntityMask(const Entity e)
        {
            REI_THROW_IF(IsDead(e), "Cannot get mask of dead entity");
            
            return _entityMasks[e.Id];
        }

        template <typename T>
        T& GetComponent(Entity& e)
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
        bool HasComponent(const Entity& e)
        {
            REI_THROW_IF(IsDead(e), "Cannot check if dead entity has component")
            
            auto componentSet = GetSet<T>();
            return componentSet->Has(e);
        }

        template <typename T>
        void DeleteComponent(Entity& e)
        {
            REI_THROW_IF(IsDead(e), "Cannot delete component on dead entity")
        
            auto set = GetSet<T>();
            if (set->Delete(e))
            {
                GetEntityMask(e).Remove(set->Id());
                _dirtyEntities.insert(e);
            }
        }

        bool IsAlive(const Entity e) const
        {
            return _entities[e.Id].Generation == e.Generation;
        }
        
        bool IsDead(const Entity e) const
        {
            return !IsAlive(e);
        }

        void DestroyEntity(const Entity e)
        {
            GetEntityMask(e).Clear();
            _dirtyEntities.insert(e);
            _destroyedEntities.insert(e);
        }

        void HandleDeadEntity(const Entity e)
        {
            _deadEntities.push(e);
            _entities[e.Id].Generation = 0;
        }
        
        Entity GetEntityById(const EntityId id) const
        {
            return _entities[id];
        }
        
        template <typename T>
        std::shared_ptr<ComponentSet<T>> GetSet()
        {
            const u32 componentId = TypeId::Get<T>();
            for (int i = _componentSets.size(); i <= componentId; i++)
            {
                CreateComponentSet<T>(i);
            }

            return std::static_pointer_cast<ComponentSet<T>>(_componentSets.at(componentId));
        }

        const std::unordered_set<Entity>& GetDirtyEntities() const { return _dirtyEntities; }
        const std::unordered_set<Entity>& GetDestroyedEntities() const { return _destroyedEntities; }
        const std::vector<std::shared_ptr<ISet>>& GetComponentSets() const { return _componentSets; }

        void ClearDirtyEntities() { _dirtyEntities.clear(); }
        void ClearDestroyedEntities() { _destroyedEntities.clear(); }

        void ResizeMasks(const u32 size)
        {
            for (auto& entityMask : _entityMasks)
            {
                entityMask.Resize(size);
            }
        }

    private:
        std::vector<std::shared_ptr<ISet>> _componentSets{};
        std::unordered_set<Entity> _dirtyEntities{};
        std::unordered_set<Entity> _destroyedEntities{};
        std::queue<Entity> _deadEntities{};

        std::vector<Entity> _entities{};
        std::vector<BitMask> _entityMasks{};

        u64 _maxComponentId = 0;
        EntityGen _currentGeneration = 1;

        template <typename T>
        std::shared_ptr<ComponentSet<T>> CreateComponentSet(u64 id)
        {
            auto set = std::make_shared<ComponentSet<T>>(id);
            _componentSets.push_back(std::static_pointer_cast<ISet>(set));

            if (_maxComponentId < set->Id())
            {
                _maxComponentId = set->Id();
                MaxComponentIdChangedEvent.Invoke(_maxComponentId);
            }

            return set;
        }
    };
}
