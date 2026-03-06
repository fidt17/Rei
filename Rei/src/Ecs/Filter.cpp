#include "pch.h"
#include "Filter.h"
#include "Entity.h"

namespace rei::ecs
{
    const std::vector<Entity>& Filter::Entities() const
    {
        return _entitiesList;
    }

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

    void Filter::ResizeMask(const size_t size)
    {
        _includeMask.Resize(size);
        _excludeMask.Resize(size);
    }

    void Filter::Include(const BitMask& m)
    {
        _includeMask = m;
    }

    void Filter::Exclude(const BitMask& m)
    {
        _excludeMask = m;
    }

    const BitMask& Filter::GetIncludeMask() const
    {
        return _includeMask;
    }

    const BitMask& Filter::GetExcludeMask() const
    {
        return _excludeMask;
    }

    size_t Filter::GetEntitiesCount() const
    {
        return _entitiesList.size();
    }

    std::vector<Entity>::iterator Filter::begin()
    {
        return _entitiesList.begin();
    }

    std::vector<Entity>::iterator Filter::end()
    {
        return _entitiesList.end();
    }

    Entity Filter::First() const
    {
        if (_entitiesList.size() == 0) return NULL_ENTITY;
        return _entitiesList[0];
    }

    bool Filter::IsValid(const BitMask& mask) const
    {
        return _includeMask.All(mask) && !_excludeMask.Any(mask);
    }
}
