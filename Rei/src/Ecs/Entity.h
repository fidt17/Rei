#pragma once
#include "BitMask.h"

namespace rei::ecs
{
    using EntityId = i32;
    using EntityGen = u8;
    constexpr i32 ENTITIES_PER_GENERATION = 2147483648 - 1;

    struct Entity
    {
        Entity(const EntityId id, const EntityGen gen)
            : Id(id), Generation(gen)
        {
        }

        EntityId Id = -1;
        EntityGen Generation = 0;
        BitMask ComponentsMask;

        bool operator==(const Entity& other) const
        {
            return Id == other.Id && Generation == other.Generation;
        }
    };
}

template <>
struct std::hash<rei::ecs::Entity>
{
    std::size_t operator()(const rei::ecs::Entity& e) const noexcept
    {
        return e.Id * 1337 ^ e.Generation;
    }
};
