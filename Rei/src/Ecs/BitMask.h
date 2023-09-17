#pragma once

class BitMask
{
public:
    void Set(const u64 flagIdx)
    {
        _flags.at(GetLayerIdx(flagIdx)) |= static_cast<u64>(1) << flagIdx;
    }

    void Clear(const u64 flagIdx)
    {
        _flags.at(GetLayerIdx(flagIdx)) &= ~(static_cast<u64>(1) << flagIdx);
    }

    void Resize(const u64 size)
    {
        if (_flags.size() >= size) return;
        _flags.resize(size);
    }

    bool All(const BitMask& other) const
    {
        for (u64 i = 0; i < _flags.size(); i++)
        {
            const u64 flag = _flags[i];
            if (flag != (flag & other._flags[i]))
            {
                return false;
            }
        }

        return true;
    }

    bool Any(const BitMask& other) const
    {
        for (u64 i = 0; i < _flags.size(); i++)
        {
            const u64 flag = _flags[i];
            if ((flag & other._flags[i]) != 0) return true;
        }

        return false;
    }

private:
    std::vector<u64> _flags{0};

    u64 GetLayerIdx(const u64 idx) const { return idx / 64; }
};
