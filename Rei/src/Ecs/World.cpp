#include "pch.h"
#include "World.h"
#include "Entity.h"

namespace rei::ecs
{
    Entity& World::NewEntity()
    {
        if (_lastId == ENTITIES_PER_GENERATION)
        {
            _lastId = 0;
            _currentGeneration += 1;
        }

        const auto id = _lastId++;
        const auto gen = _currentGeneration;

        _entities.emplace_back(id, gen);

        return _entities.back();
    }

    std::shared_ptr<Filter> World::CreateFilter()
    {
        auto filter = std::make_shared<Filter>();
        _filters.push_back(filter);
        return filter;
    }

    void World::UpdateWorld()
    {
        for (const auto& changedEntity : _changedEntities)
        {
            for (const auto& filter : _filters)
            {
                filter->OnEntityChange(changedEntity);
            }
        }
        _changedEntities.clear();
    }
}
