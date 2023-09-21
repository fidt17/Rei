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
            (_excludeMask.Remove(TypeId::Get<Ts>()), ...);
            
            return shared_from_this();
        }
        
        template <typename... Ts>
        std::shared_ptr<Filter> Exclude()
        {
            (_includeMask.Remove(TypeId::Get<Ts>()), ...);
            (_excludeMask.Set(TypeId::Get<Ts>()), ...);
            
            return shared_from_this();
        }

        void OnEntityChange(const Entity& e, const BitMask& entityMask);

        void ResizeMask(u64 size);
        
        const BitMask& GetIncludeMask() const;
        const BitMask& GetExcludeMask() const;

    private:
        BitMask _includeMask; // todo: resize
        BitMask _excludeMask; // todo: resize
        std::unordered_set<EntityId> _entitiesSet;
        std::vector<EntityId> _entitiesList;

        bool IsValid(const BitMask& entityMask) const;
    };
}
