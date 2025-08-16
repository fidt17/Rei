#pragma once

namespace rei::ecs
{
    class BitMask
    {
    public:
        typedef u64 mask;

        REI_API void Set(mask flagIdx, bool resizeIfNeeded = false);
        REI_API void Remove(mask flagIdx);
        REI_API void Resize(size_t size);
        REI_API void Clear();

        REI_API bool All(const BitMask& other) const;
        REI_API bool Any(const BitMask& other) const;

        REI_API size_t Size() const;
        REI_API std::string ToString() const;

        REI_API bool operator==(const BitMask& other) const;

        REI_API const std::vector<mask>& GetFlags() const;

    private:
        std::vector<mask> _flags{0};

        u32 GetLayerIdx(mask idx) const;
    };
}
