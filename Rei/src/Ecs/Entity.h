#pragma once

namespace rei::ecs
{
    using EntityId = i32;
    using EntityGen = u8;

    struct Entity
    {
        Entity(const EntityId id, const EntityGen gen)
            : Id(id), Generation(gen)
        {
        }
        
        EntityId Id = -1;
        EntityGen Generation = 0;
        
        bool operator==(const Entity& other) const;
        std::string ToString() const;
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
