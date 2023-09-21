#pragma once


namespace rei::ecs
{
    class BitMask
    {
    public:
        typedef u64 mask;
        
        void Set(mask flagIdx);
        void Remove(mask flagIdx);
        void Resize(u32 size);
        void Clear();

        bool All(const BitMask& other) const;
        bool Any(const BitMask& other) const;

        std::string ToString() const;

    private:
        std::vector<mask> _flags{0};

        u32 GetLayerIdx(mask idx) const;
    };
}
