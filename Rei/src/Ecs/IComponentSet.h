#pragma once
#include "Entity.h"

namespace rei::ecs
{
    class IComponentSet
    {
    public:
        virtual ~IComponentSet() = default;

        virtual u64 Id() const = 0;
        virtual bool Has(Entity e) const = 0;
        virtual bool Delete(Entity e) = 0;
    };
}
