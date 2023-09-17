#pragma once
#include <unordered_set>

#include "ComponentSet.h"
#include "Filter.h"
#include "TypeId.h"
#include "Entity.h"

namespace rei::ecs
{
    class World
    {
    public:
        World()
            : _lastId(0),
              _currentGeneration(0)
        {
        }

        Entity& NewEntity();

        template <typename T>
        T& GetComponent(const EntityId entityId)
        {
            return GetComponent<T>(_entities[entityId]);
        }

        template <typename T>
        T& GetComponent(Entity* entity)
        {
            return GetComponent<T>(_entities[entity->Id]);
        }

        template <typename T>
        T& GetComponent(Entity& e)
        {
            auto componentSet = GetSet<T>();

            bool didAddComponent = false;
            auto& component = componentSet->Get(e, didAddComponent);

            if (didAddComponent)
            {
                _changedEntities.insert(e);
            }

            return component;
        }

        template <typename T>
        bool HasComponent(const Entity& e)
        {
            return GetSet<T>()->Has(e);
        }

        template <typename T>
        void DeleteComponent(EntityId entityId)
        {
            return DeleteComponent<T>(_entities[entityId]);
        }

        template <typename T>
        void DeleteComponent(Entity& e)
        {
            if (GetSet<T>()->Delete(e))
            {
                _changedEntities.insert(e);
            }
        }

        std::shared_ptr<Filter> CreateFilter();

        template <typename T>
        void Include(const std::shared_ptr<Filter>& f)
        {
            f->Include(GetSet<T>());
        }

        template <typename T>
        void Exclude(const std::shared_ptr<Filter>& f)
        {
            f->Exclude(GetSet<T>());
        }

        void UpdateWorld();

    private:
        EntityId _lastId;
        EntityGen _currentGeneration;

        std::vector<Entity> _entities;
        std::vector<std::shared_ptr<ISet>> _componentSets{};
        std::vector<std::shared_ptr<Filter>> _filters{};
        std::unordered_set<Entity> _changedEntities{};

        template <typename T>
        std::shared_ptr<ComponentSet<T>> CreateComponentSet(u64 id)
        {
            auto set = std::make_shared<ComponentSet<T>>(id);
            _componentSets.push_back(std::static_pointer_cast<ISet>(set));
            return set;
        }

        template <typename T>
        std::shared_ptr<ComponentSet<T>> GetSet()
        {
            const u32 componentId = TypeId::Get<T>();
            if (_componentSets.size() <= componentId)
            {
                return CreateComponentSet<T>(componentId);
            }

            return std::static_pointer_cast<ComponentSet<T>>(_componentSets.at(componentId));
        }
    };
}
