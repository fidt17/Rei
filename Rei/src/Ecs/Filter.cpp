#include "pch.h"
#include "Filter.h"
#include "Entity.h"

namespace rei::ecs
{
    void Filter::OnEntityChange(const Entity e, const BitMask& entityMask)
    {
        const bool isValid = IsValid(entityMask);
        const bool exists = _entitiesSet.count(e);

        if (isValid && !exists)
        {
            _entitiesSet.insert(e);
            _entitiesList.push_back(e);
        }
        else if (!isValid && exists)
        {
            _entitiesSet.erase(e);
            _entitiesList.erase(std::remove(_entitiesList.begin(), _entitiesList.end(), e), _entitiesList.end());
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

    bool Filter::IsValid(const BitMask& mask) const
    {
        return _includeMask.All(mask) && !_excludeMask.Any(mask);
    }
}
