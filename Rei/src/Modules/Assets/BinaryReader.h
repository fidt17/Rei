#pragma once
#include <fstream>

namespace rei::assets
{
    class BinaryReader
    {
    public:
        REI_API explicit BinaryReader(const std::string& path);

        REI_API void SetPosition(i64 position);
        
        REI_API u8 GetU8();
        REI_API u16 GetU16();
        REI_API u32 GetU32();
        REI_API u64 GetU64();

        REI_API i8 GetI8();
        REI_API i16 GetI16();
        REI_API i32 GetI32();
        REI_API i64 GetI64();

        REI_API f32 GetF32();

        REI_API std::string GetStr();

        template <typename T>
        REI_API T Get()
        {
            return T(*this);
        }

    private:
        std::ifstream _stream;

        template <typename T>
        T GetByType()
        {
            T value;
            _stream.read(reinterpret_cast<char*>(&value), sizeof value);
            return value;
        }
    };
}
