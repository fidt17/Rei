#include "pch.h"
#include "BinaryReader.h"

namespace rei::assets
{
    BinaryReader::BinaryReader(const std::string& path)
    {
        _stream = std::ifstream(path, std::ios::in | std::ios::binary);
        REI_THROW_IF(_stream.bad(), "Could not open stream for " + path)
    }

    void BinaryReader::SetPosition(const i64 position)
    {
        _stream.seekg(position);
    }

    u8 BinaryReader::GetU8() { return GetByType<u8>(); } 

    u16 BinaryReader::GetU16() { return GetByType<u16>(); } 

    u32 BinaryReader::GetU32() { return GetByType<u32>(); } 

    u64 BinaryReader::GetU64() { return GetByType<u64>(); } 

    i8 BinaryReader::GetI8() { return GetByType<i8>(); } 

    i16 BinaryReader::GetI16() { return GetByType<i16>(); } 

    i32 BinaryReader::GetI32() { return GetByType<i32>(); } 

    i64 BinaryReader::GetI64() { return GetByType<i64>(); } 

    f32 BinaryReader::GetF32() { return GetByType<f32>(); } 

    std::string BinaryReader::GetStr()
    {
        const i32 len = GetI32();
        std::string str;
        str.resize(len);
        _stream.read(str.data(), len);
        return str;
    } 
}
