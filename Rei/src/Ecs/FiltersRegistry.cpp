#include "pch.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    void FiltersRegistry::HandleEntityChange(const Entity e, const BitMask& mask) const
    {
        for (const auto& filter : _filters)
        {
            filter->OnEntityChange(e, mask);
        }
    }

    void FiltersRegistry::ResizeMasks(const size_t size) const
    {
        for (auto& filter : _filters)
        {
            filter->ResizeMask(size);
        }
    }

    u32 FiltersRegistry::GetFiltersCount() const
    {
        return static_cast<u32>(_filters.size());
    }

    std::shared_ptr<Filter> FiltersRegistry::GetFilter(const BitMask& includeMask, const BitMask& excludeMask)
    {
        for (auto f : _filters)
        {
            if (f->GetIncludeMask() == includeMask && f->GetExcludeMask() == excludeMask)
            {
                return f;
            }
        }

        auto f = std::make_shared<Filter>();
        f->Include(includeMask);
        f->Exclude(excludeMask);
        ResizeMasks(std::max(includeMask.Size(), excludeMask.Size()));
            
        _filters.push_back(std::move(f));
        return _filters.back();
    }
}
