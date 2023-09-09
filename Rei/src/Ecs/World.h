#pragma once
#include <set>
#include <unordered_set>

#include "ComponentSet.h"
#include "Ecs.h"
#include "TypeId.h"

namespace rei::ecs
{
    struct Filter
    {
        std::vector<std::shared_ptr<ISet>> IncludeSets;
        std::vector<std::shared_ptr<ISet>> ExcludeSets;
        std::unordered_set<Entity> EntitiesSet;
        std::vector<Entity> EntitiesList;

        bool IsValid(const Entity e) const
        {
            for (const auto& includeSet : IncludeSets)
            {
                if (!includeSet->Has(e))
                {
                    return false;
                }
            }

            for (const auto& excludeSet : ExcludeSets)
            {
                if (excludeSet->Has(e))
                {
                    return false;
                }
            }

            return true;
        }

        void OnEntityChange(const Entity e)
        {
            const bool isValid = IsValid(e);
            const bool exists = EntitiesSet.count(e);

            if (isValid && !exists)
            {
                EntitiesSet.insert(e);
                EntitiesList.push_back(e);
            }
            else if (!isValid && exists)
            {
                EntitiesSet.erase(e);
                EntitiesList.erase(std::remove(EntitiesList.begin(), EntitiesList.end(), e), EntitiesList.end());
            }
        }
    };

    class World
    {
    public:
        World()
            : _lastId(0), _currentGeneration(0)
        {
        }

        Entity CreateEntity()
        {
            if (_lastId == ENTITIES_PER_GENERATION)
            {
                _lastId = 0;
                _currentGeneration += 1;
            }

            const auto id = _lastId++;
            const auto gen = _currentGeneration;

            return {id, gen};
        }

        template <typename T>
        void AddComponent(Entity e)
        {
            _changedEntities.insert(e);
            GetSet<T>()->Get(e);
        }

        template <typename T>
        T& GetComponent(Entity e)
        {
            _changedEntities.insert(e);
            return GetSet<T>()->Get(e);
        }

        template <typename T>
        bool HasComponent(Entity e)
        {
            return GetSet<T>()->Has(e);
        }

        template <typename T>
        void DeleteComponent(Entity e)
        {
            _changedEntities.insert(e);
            GetSet<T>()->Delete(e);
        }

        template <typename T>
        void PrintSet() { GetSet<T>()->PrintSet(); }

        std::shared_ptr<Filter> CreateFilter()
        {
            auto filter = std::make_shared<Filter>();
            _filters.push_back(filter);
            return filter;
        }

        template <typename T>
        void Include(const std::shared_ptr<Filter> f)
        {
            f->IncludeSets.push_back(GetSet<T>());
        }

        template <typename T>
        void Exclude(const std::shared_ptr<Filter> f)
        {
            f->ExcludeSets.push_back(GetSet<T>());
        }

        void UpdateWorld()
        {
            for (const auto changedEntity : _changedEntities)
            {
                for (const auto& filter : _filters)
                {
                    filter->OnEntityChange(changedEntity);
                }
            }
            _changedEntities.clear();
        }

    private:
        EntityId _lastId;
        EntityGen _currentGeneration;

        std::vector<std::shared_ptr<ISet>> _componentSets{};
        std::vector<std::shared_ptr<Filter>> _filters{};
        std::unordered_set<Entity> _changedEntities{};

        template <typename T>
        std::shared_ptr<ComponentSet<T>> CreateComponentSet()
        {
            auto set = std::make_shared<ComponentSet<T>>();
            _componentSets.push_back(std::static_pointer_cast<ISet>(set));
            return set;
        }

        template <typename T>
        std::shared_ptr<ComponentSet<T>> GetSet()
        {
            const u32 componentId = TypeId::Get<T>();
            if (_componentSets.size() <= componentId)
            {
                return CreateComponentSet<T>();
            }

            return std::static_pointer_cast<ComponentSet<T>>(_componentSets.at(componentId));
        }
    };
}
