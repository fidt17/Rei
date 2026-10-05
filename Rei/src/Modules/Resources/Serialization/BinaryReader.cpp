#include "pch.h"
#include "BinaryReader.h"
#include <memory>

namespace rei::resources
{
    BinaryReader::BinaryReader(const std::string& path, const i64 pos)
    {
        _stream.open(path, std::ios::in | std::ios::binary);
        REI_THROW_IF(_stream.fail(), "Could not open stream for " + path)
        SetPosition(pos);
    }

    void BinaryReader::SetPosition(const i64 position)
    {
        REI_THROW_IF(position < 0, "Negative binary stream position")
        REI_THROW_IF(!_stream.is_open() || _stream.fail(), "Binary stream is not readable")
        _stream.seekg(position);
        REI_THROW_IF(_stream.fail(), "Could not seek binary stream")
    }

    i64 BinaryReader::GetPosition()
    {
        return _stream.tellg();
    }

    void BinaryReader::Close()
    {
        _stream.close();
    }

    u8* BinaryReader::GetBytes(i32& length)
    {
        length = GetI32();
        ValidateLength(length, sizeof(u8));
        std::unique_ptr<u8[]> bytes(new u8[length]);
        ReadData(reinterpret_cast<char*>(bytes.get()), length);

        return bytes.release();
    }

    void BinaryReader::ReadData(char* bytes, const i64 length)
    {
        REI_THROW_IF(!_stream.is_open() || _stream.fail(), "Binary stream is not readable")
        _stream.read(bytes, length);
        REI_THROW_IF(_stream.fail(), "Could not read complete binary data")
    }

    void BinaryReader::ValidateLength(const i32 count, const u64 elementSize)
    {
        REI_THROW_IF(count < 0, "Negative binary payload length")
        if (count == 0) return;

        const auto position = _stream.tellg();
        REI_THROW_IF(position == std::streampos(-1), "Could not read binary stream position")
        _stream.seekg(0, std::ios::end);
        const auto end = _stream.tellg();
        const bool endFailed = _stream.fail();
        _stream.clear();
        _stream.seekg(position);
        REI_THROW_IF(endFailed || _stream.fail() || end < position, "Could not inspect binary stream length")
        REI_THROW_IF(static_cast<u64>(count) > static_cast<u64>(end - position) / elementSize, "Binary payload exceeds remaining data")
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
        ValidateLength(len, sizeof(char));
        std::string str;
        str.resize(len);
        ReadData(str.data(), len);
        return str;
    } 
}
