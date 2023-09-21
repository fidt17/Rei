#pragma once
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
        
        Entity NewEntity(const EntityId id, const EntityGen gen)
        {
            _entities.emplace_back(id, gen);
            _entityMasks.emplace_back().Resize(_maxComponentId);
            return _entities.back();
        }

        BitMask& GetEntityMask(const Entity e)
        {
            return _entityMasks[e.Id];
        }

        template <typename T>
        T& GetComponent(Entity& e)
        {
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
            auto componentSet = GetSet<T>();
            return componentSet->Has(e);
        }

        template <typename T>
        void DeleteComponent(Entity& e)
        {
            auto set = GetSet<T>();
            if (set->Delete(e))
            {
                GetEntityMask(e).Remove(set->Id());
                _dirtyEntities.insert(e);
            }
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
        void ClearDirtyEntities() { _dirtyEntities.clear(); }

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

        std::vector<Entity> _entities{};
        std::vector<BitMask> _entityMasks{};
        
        u64 _maxComponentId = 0;

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
