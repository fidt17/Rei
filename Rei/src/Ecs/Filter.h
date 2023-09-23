#pragma once
#include <unordered_set>

#include "BitMask.h"
#include "ComponentSet.h"

namespace rei::ecs
{
    class Filter : public std::enable_shared_from_this<Filter>
    {
    public:
        const std::vector<Entity>& Entities() const;
        void OnEntityChange(Entity e, const BitMask& entityMask);

        void ResizeMask(u64 size);

        void Include(const BitMask&);
        void Exclude(const BitMask&);
        const BitMask& GetIncludeMask() const;
        const BitMask& GetExcludeMask() const;

        std::vector<Entity>::iterator begin();
        std::vector<Entity>::iterator end();

    private:
        BitMask _includeMask;
        BitMask _excludeMask;
        std::unordered_set<Entity> _entitiesSet;
        std::vector<Entity> _entitiesList;

        bool IsValid(const BitMask& mask) const;
    };
}
