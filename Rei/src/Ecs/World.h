#pragma once
#include <unordered_set>

#include "EcsRegistry.h"
#include "Entity.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    class EcsRegistry;
    class FiltersRegistry;

    class World : public std::enable_shared_from_this<World>
    {
    public:
        World()
            : _ecsRegistry(std::make_shared<EcsRegistry>()),
              _filterRegistry(std::make_shared<FiltersRegistry>()),
              _lastId(0),
              _currentGeneration(0)
        {
        }

        Entity& NewEntity();
        std::shared_ptr<Filter> NewFilter() const { return _filterRegistry->CreateFilter(); }

        void Refresh() const;

        std::shared_ptr<EcsRegistry> GetRegistry() { return _ecsRegistry; }

        
        std::shared_ptr<FiltersRegistry> GetFiltersRegistry() { return _filterRegistry; }

    private:
        std::shared_ptr<EcsRegistry> _ecsRegistry;
        std::shared_ptr<FiltersRegistry> _filterRegistry;
        EntityId _lastId;
        EntityGen _currentGeneration;

        std::vector<Entity> _entities;
    };
}
