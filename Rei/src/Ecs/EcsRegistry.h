#pragma once
#include <unordered_set>

#include "ComponentSet.h"
#include "TypeId.h"

namespace rei::ecs
{
    class World;

    class EcsRegistry
    {
    public:
        template <typename T>
        T& GetComponent(Entity& e)
        {
            auto componentSet = GetSet<T>();

            bool didAddComponent = false;
            auto& component = componentSet->Get(e, didAddComponent);

            if (didAddComponent)
            {
                _dirtyEntities.insert(e);
            }

            return component;
        }

        template <typename T>
        bool HasComponent(const Entity& e)
        {
            return GetSet<T>()->Has(e);
        }

        template <typename T>
        void DeleteComponent(Entity& e)
        {
            if (GetSet<T>()->Delete(e))
            {
                _dirtyEntities.insert(e);
            }
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

        const std::unordered_set<Entity>& GetDirtyEntities() const { return _dirtyEntities; }
        void ClearDirtyEntities() { _dirtyEntities.clear(); }

    private:
        std::vector<std::shared_ptr<ISet>> _componentSets{};
        std::unordered_set<Entity> _dirtyEntities{};

        template <typename T>
        std::shared_ptr<ComponentSet<T>> CreateComponentSet(u64 id)
        {
            auto set = std::make_shared<ComponentSet<T>>(id);
            _componentSets.push_back(std::static_pointer_cast<ISet>(set));
            return set;
        }
    };
}
