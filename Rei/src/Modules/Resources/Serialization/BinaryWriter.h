#pragma once
#include "Core.h"
#include <fstream>

namespace rei::resources
{
    class BinaryWriter
    {
    public:
        REI_API explicit BinaryWriter(const std::string& path, i64 pos);

        REI_API void SetPosition(i64 position);
        REI_API i64 GetPosition();
        REI_API void Close();

        REI_API void WriteBytes(const unsigned char* bytes, i32 length);
        
        REI_API void WriteU8(u8);
        REI_API void WriteU16(u16);
        REI_API void WriteU32(u32);
        REI_API void WriteU64(u64);

        REI_API void WriteI8(i8);
        REI_API void WriteI16(i16);
        REI_API void WriteI32(i32);
        REI_API void WriteI64(i64);

        REI_API void WriteF32(f32);

        REI_API void WriteStr(const std::string&);

        template <typename T>
        void Write(T value)
        {
            _stream.write(reinterpret_cast<char*>(&value), sizeof value);
        }
        
    private:
        std::ofstream _stream;
    };
}
