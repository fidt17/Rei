#include "pch.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    std::shared_ptr<Filter> FiltersRegistry::NewFilter()
    {
        _filters.push_back(std::make_shared<Filter>());
        return _filters.back();
    }

    void FiltersRegistry::HandleEntityChange(const Entity e, const BitMask& mask) const
    {
        for (const auto& filter : _filters)
        {
            filter->OnEntityChange(e, mask);
        }
    }

    void FiltersRegistry::ResizeMasks(const u32 size) const
    {
        for (auto& filter : _filters)
        {
            filter->ResizeMask(size);
        }
    }
}
