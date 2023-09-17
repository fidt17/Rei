#pragma once
#include <unordered_set>

#include "BitMask.h"
#include "ComponentSet.h"

namespace rei::ecs
{
    class Filter
    {
    public:
        const std::vector<EntityId>& Entities() const { return _entitiesList; }
        
        void Include(const std::shared_ptr<ISet>& set);

        void Exclude(const std::shared_ptr<ISet>& set);

        void OnEntityChange(const Entity& e);

    private:
        BitMask _includeMask; // todo: resize
        BitMask _excludeMask; // todo: resize
        std::vector<std::shared_ptr<ISet>> _includeSets;
        std::unordered_set<EntityId> _entitiesSet;
        std::vector<EntityId> _entitiesList;

        bool IsValid(const Entity& e) const;
    };
}
