#include "pch.h"
#include "BinaryWriter.h"

namespace rei::resources
{
    BinaryWriter::BinaryWriter(const std::string& path, const i64 pos)
    {
        _stream.open(path, std::fstream::in | std::fstream::out | std::fstream::binary);
        REI_THROW_IF(_stream.bad(), "Could not open stream for " + path)
        SetPosition(pos);
    }

    void BinaryWriter::Close()
    {
        _stream.flush();
        _stream.close();
    }

    void BinaryWriter::WriteBytes(const unsigned char* bytes, const i32 length)
    {
        WriteI32(length);
        _stream.write(reinterpret_cast<const char*>(bytes), length);
    }

    void BinaryWriter::SetPosition(const i64 position)
    {
        _stream.seekp(position);
    }

    i64 BinaryWriter::GetPosition()
    {
        return _stream.tellp();
    }

    void BinaryWriter::WriteU8(const u8 value) { Write(value); }

    void BinaryWriter::WriteU16(const u16 value) { Write(value); }

    void BinaryWriter::WriteU32(const u32 value) { Write(value); }

    void BinaryWriter::WriteU64(const u64 value) { Write(value); }

    void BinaryWriter::WriteI8(const i8 value) { Write(value); }

    void BinaryWriter::WriteI16(const i16 value) { Write(value); }

    void BinaryWriter::WriteI32(const i32 value) { Write(value); }

    void BinaryWriter::WriteI64(const i64 value) { Write(value); }

    void BinaryWriter::WriteF32(const f32 value) { Write(value); }

    void BinaryWriter::WriteStr(const std::string& value)
    {
        const i32 size = static_cast<i32>(value.length());
        WriteI32(size);
        _stream.write(value.data(), size);
    }
}

