#pragma once
#include "Filter.h"

namespace rei::ecs
{
    class FiltersRegistry
    {
    public:
        std::shared_ptr<Filter> NewFilter();

        void HandleEntityChange(Entity e, const BitMask& mask) const;
        void ResizeMasks(u32 size) const;

    private:
        std::vector<std::shared_ptr<Filter>> _filters;
    };
}
