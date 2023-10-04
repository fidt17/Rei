#pragma once
#include <fstream>

namespace rei::assets
{
    class BinaryReader
    {
    public:
        explicit BinaryReader(const std::string& path);

        u8 GetU8();
        u16 GetU16();
        u32 GetU32();
        u64 GetU64();

        i8 GetI8();
        i16 GetI16();
        i32 GetI32();
        i64 GetI64();

        f32 GetF32();

        std::string GetStr();

        template <typename T>
        T Get()
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
