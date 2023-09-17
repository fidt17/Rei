#include "pch.h"
#include "Filter.h"
#include "Entity.h"

namespace rei::ecs
{
    void Filter::Include(const std::shared_ptr<ISet>& set)
    {
        // todo: check for duplicates
        _includeSets.push_back(set);
        _includeMask.Set(set->Id());
    }

    void Filter::Exclude(const std::shared_ptr<ISet>& set)
    {
        _excludeMask.Set(set->Id());
    }

    void Filter::OnEntityChange(const Entity& e)
    {
        const EntityId entityId = e.Id;;
        const bool isValid = IsValid(e);
        const bool exists = _entitiesSet.count(entityId);

        if (isValid && !exists)
        {
            _entitiesSet.insert(entityId);
            _entitiesList.push_back(entityId);
        }
        else if (!isValid && exists)
        {
            _entitiesSet.erase(entityId);
            _entitiesList.erase(std::remove(_entitiesList.begin(), _entitiesList.end(), entityId), _entitiesList.end());
        }
    }

    bool Filter::IsValid(const Entity& e) const
    {
        return _includeMask.All(e.ComponentsMask) && !_excludeMask.Any(e.ComponentsMask);
    }
}
