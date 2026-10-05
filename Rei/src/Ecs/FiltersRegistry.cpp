#include "pch.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    void FiltersRegistry::HandleEntityChange(const Entity e, const BitMask& mask, const bool isAlive) const
    {
        for (const auto& filter : _filters)
        {
            filter->OnEntityChange(e, mask, isAlive);
        }
    }

    void FiltersRegistry::ResizeMasks(const size_t size) const
    {
        _maxMaskBitIndex = std::max(_maxMaskBitIndex, size);
        for (auto& filter : _filters)
        {
            filter->ResizeMask(_maxMaskBitIndex);
        }
    }

    u32 FiltersRegistry::GetFiltersCount() const
    {
        return static_cast<u32>(_filters.size());
    }

    std::shared_ptr<Filter> FiltersRegistry::GetFilter(const BitMask& includeMask, const BitMask& excludeMask)
    {
        constexpr size_t BITS_PER_WORD = sizeof(BitMask::mask) * 8;
        const auto maskSize = std::max({includeMask.Size(), excludeMask.Size(), _maxMaskBitIndex / BITS_PER_WORD + 1});
        const auto maxBitIndex = maskSize * BITS_PER_WORD - 1;
        auto normalizedInclude = includeMask;
        auto normalizedExclude = excludeMask;
        normalizedInclude.Resize(maxBitIndex);
        normalizedExclude.Resize(maxBitIndex);

        for (auto f : _filters)
        {
            auto existingInclude = f->GetIncludeMask();
            auto existingExclude = f->GetExcludeMask();
            existingInclude.Resize(maxBitIndex);
            existingExclude.Resize(maxBitIndex);
            if (existingInclude == normalizedInclude && existingExclude == normalizedExclude)
            {
                return f;
            }
        }

        ResizeMasks(maxBitIndex);
        auto f = std::make_shared<Filter>();
        f->Include(normalizedInclude);
        f->Exclude(normalizedExclude);
            
        _filters.push_back(std::move(f));
        NewFilterCreatedEvent();
        return _filters.back();
    }
}
