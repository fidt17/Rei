#pragma once
#include <unordered_set>

#include "BitMask.h"
#include "ComponentSet.h"
#include "TypeId.h"

namespace rei::ecs
{
    class Filter : public std::enable_shared_from_this<Filter>
    {
    public:
        const std::vector<EntityId>& Entities() const { return _entitiesList; }

        template <typename... Ts>
        std::shared_ptr<Filter> Include()
        {
            (_includeMask.Set(TypeId::Get<Ts>()), ...);
            (_excludeMask.Clear(TypeId::Get<Ts>()), ...);
            
            return shared_from_this();
        }
        
        template <typename... Ts>
        std::shared_ptr<Filter> Exclude()
        {
            (_includeMask.Clear(TypeId::Get<Ts>()), ...);
            (_excludeMask.Set(TypeId::Get<Ts>()), ...);
            
            return shared_from_this();
        }

        void OnEntityChange(const Entity& e);

    private:
        BitMask _includeMask; // todo: resize
        BitMask _excludeMask; // todo: resize
        std::unordered_set<EntityId> _entitiesSet;
        std::vector<EntityId> _entitiesList;

        bool IsValid(const Entity& e) const;
    };
}
