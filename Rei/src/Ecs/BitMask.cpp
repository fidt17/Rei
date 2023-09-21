#include "pch.h"
#include "BitMask.h"

namespace rei::ecs
{
    void BitMask::Set(const mask flagIdx)
    {
        REI_ASSERT(flagIdx < sizeof(mask) * 8 * _flags.size(), 
        "FlagIdx is too large. Idx: " + std::to_string(flagIdx) + ". Mask size: " + std::to_string(_flags.size()))
        
        const auto layerIdx = GetLayerIdx(flagIdx);
        _flags.at(layerIdx) |= static_cast<mask>(1) << flagIdx;
    }

    void BitMask::Remove(const mask flagIdx)
    {
        REI_ASSERT(flagIdx < sizeof(mask) * 8 * _flags.size(), 
        "FlagIdx is too large. Idx: " + std::to_string(flagIdx) + ". Mask size: " + std::to_string(_flags.size()))
        
        const auto layerIdx = GetLayerIdx(flagIdx);
        _flags.at(layerIdx) &= ~(static_cast<mask>(1) << flagIdx);
    }

    bool BitMask::All(const BitMask& other) const
    {
        REI_ASSERT(_flags.size() == other._flags.size(), 
        "Sizes differ. This size: " + std::to_string(_flags.size()) + ". Other size: " + std::to_string(other._flags.size()))
    
        for (mask i = 0; i < _flags.size(); i++)
        {
            const mask flag = _flags[i];
            if (flag != (flag & other._flags[i]))
            {
                return false;
            }
        }

        return true;
    }

    bool BitMask::Any(const BitMask& other) const
    {
        REI_ASSERT(_flags.size() == other._flags.size(), 
        "Sizes differ. This size: " + std::to_string(_flags.size()) + ". Other size: " + std::to_string(other._flags.size()))
        
        for (mask i = 0; i < _flags.size(); i++)
        {
            const mask flag = _flags[i];
            if ((flag & other._flags[i]) != 0) return true;
        }

        return false;
    }

    void BitMask::Resize(const u32 size)
    {
        if (_flags.size() * (sizeof(mask) * 8) > size) return;
        _flags.resize(size);
    }

    void BitMask::Clear()
    {
        for (auto& flag : _flags)
        {
            flag = 0;
        }
    }

    std::string BitMask::ToString() const
    {
        std::string str;

        for (const mask _flag : _flags)
        {
            str += std::to_string(_flag);
        }

        return str;
    }

    u32 BitMask::GetLayerIdx(const mask idx) const
    {
        return static_cast<u32>(idx / (sizeof(mask) * 8));
    }
}
