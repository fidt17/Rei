#include "pch.h"
#include "BitMask.h"

namespace rei::ecs
{
    void BitMask::Set(const mask flagIdx, const bool resizeIfNeeded)
    {
        if (resizeIfNeeded)
        {
            Resize(flagIdx);
        }
        REI_ASSERT(flagIdx < sizeof(mask) * 8 * _flags.size(), std::format("FlagIdx is too large. Idx: {}. mask size: {}", flagIdx, _flags.size()))

        const auto layerIdx = GetLayerIdx(flagIdx);
        _flags.at(layerIdx) |= static_cast<mask>(1) << flagIdx;
    }

    void BitMask::Remove(const mask flagIdx)
    {
        REI_ASSERT(flagIdx < sizeof(mask) * 8 * _flags.size(), std::format("FlagIdx is too large. Idx: {}. Mask size: {}", flagIdx, _flags.size()))

        const auto layerIdx = GetLayerIdx(flagIdx);
        _flags.at(layerIdx) &= ~(static_cast<mask>(1) << flagIdx);
    }

    bool BitMask::All(const BitMask& other) const
    {
        REI_ASSERT(_flags.size() == other._flags.size(), std::format("Sizes differ. This size: {}. Other size: {}", _flags.size(), other._flags.size()))

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
        REI_ASSERT(_flags.size() == other._flags.size(), std::format("Sizes differ. This size: {}. Other size: {}", _flags.size(), other._flags.size()))

        for (mask i = 0; i < _flags.size(); i++)
        {
            const mask flag = _flags[i];
            if ((flag & other._flags[i]) != 0) return true;
        }

        return false;
    }

    size_t BitMask::Size() const
    {
        return _flags.size();
    }

    void BitMask::Resize(const size_t size)
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

    bool BitMask::operator==(const BitMask& other) const
    {
        REI_ASSERT(_flags.size() == other._flags.size(), std::format("Sizes differ. This size: {}. Other size: {}", _flags.size(), other._flags.size()))

        for (auto i = 0; i < _flags.size(); i++)
        {
            if (_flags[i] != other._flags[i]) return false;
        }

        return true;
    }

    const std::vector<BitMask::mask>& BitMask::GetFlags() const
    {
        return _flags;
    }

    u32 BitMask::GetLayerIdx(const mask idx) const
    {
        return static_cast<u32>(idx / (sizeof(mask) * 8));
    }
}
