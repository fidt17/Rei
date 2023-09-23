#pragma once

namespace rei::ecs
{
    class BitMask
    {
    public:
        typedef u64 mask;

        void Set(mask flagIdx, bool resizeIfNeeded = false);
        void Remove(mask flagIdx);
        void Resize(u64 size);
        void Clear();

        bool All(const BitMask& other) const;
        bool Any(const BitMask& other) const;

        u32 Size() const;
        std::string ToString() const;

        bool operator==(const BitMask& other) const;

    private:
        std::vector<mask> _flags{0};

        u32 GetLayerIdx(mask idx) const;
    };
}
