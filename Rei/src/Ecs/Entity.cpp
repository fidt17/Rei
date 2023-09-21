#include "pch.h"
#include "Entity.h"

namespace rei::ecs
{
    bool Entity::operator==(const Entity& other) const
    {
        return Id == other.Id && Generation == other.Generation;
    }

    std::string Entity::ToString() const
    {
        return "Entity[" + std::to_string(Id) + ":" + std::to_string(Generation) + "]";
    }
}
