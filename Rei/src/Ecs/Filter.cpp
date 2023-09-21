#include "pch.h"
#include "Filter.h"
#include "Entity.h"

namespace rei::ecs
{
    void Filter::OnEntityChange(const Entity& e, const BitMask& entityMask)
    {
        const EntityId entityId = e.Id;;
        const bool isValid = IsValid(entityMask);
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

    void Filter::ResizeMask(const u64 size)
    {
        _includeMask.Resize(size);
        _excludeMask.Resize(size);
    }

    const BitMask& Filter::GetIncludeMask() const
    {
        return _includeMask;
    }

    const BitMask& Filter::GetExcludeMask() const
    {
        return _excludeMask;
    }

    bool Filter::IsValid(const BitMask& entityMask) const
    {
        return _includeMask.All(entityMask) && !_excludeMask.Any(entityMask);
    }
}
