#pragma once
#include "AssetTestSupport.h"
#include <array>

namespace rei::tests
{
    inline void AppendBigEndian(std::vector<u8>& bytes, const u32 value)
    {
        for (i32 shift = 24; shift >= 0; shift -= 8) bytes.push_back(static_cast<u8>(value >> shift));
    }

    inline u32 Crc32(const std::vector<u8>& bytes)
    {
        u32 crc = 0xffffffff;
        for (const auto byte : bytes)
        {
            crc ^= byte;
            for (u32 bit = 0; bit < 8; ++bit) crc = (crc >> 1) ^ ((crc & 1) ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    inline void AppendPngChunk(std::vector<u8>& bytes, const std::array<u8, 4>& type, const std::vector<u8>& payload)
    {
        AppendBigEndian(bytes, static_cast<u32>(payload.size()));
        std::vector<u8> chunk(type.begin(), type.end());
        chunk.insert(chunk.end(), payload.begin(), payload.end());
        bytes.insert(bytes.end(), chunk.begin(), chunk.end());
        AppendBigEndian(bytes, Crc32(chunk));
    }

    inline std::vector<u8> PngPixels(const u32 channels)
    {
        std::vector<u8> pixels(4 * channels);
        for (u32 index = 0; index < pixels.size(); ++index) pixels[index] = static_cast<u8>(index + 1);
        return pixels;
    }

    // Independent 2x2 PNG: two unfiltered rows, one stored deflate block,
    // Adler-32 and PNG CRC-32. No importer or image library creates this oracle.
    inline std::vector<u8> PngBytes(const u32 channels, const std::vector<u8>& suppliedPixels = {})
    {
        REQUIRE(channels >= 1);
        REQUIRE(channels <= 4);
        const auto pixels = suppliedPixels.empty() ? PngPixels(channels) : suppliedPixels;
        REQUIRE(pixels.size() == 4 * channels);
        std::vector<u8> raw;
        for (u32 row = 0; row < 2; ++row)
        {
            raw.push_back(0);
            raw.insert(raw.end(), pixels.begin() + row * 2 * channels, pixels.begin() + (row + 1) * 2 * channels);
        }
        const u16 length = static_cast<u16>(raw.size());
        std::vector<u8> deflate = {0x78, 0x01, 0x01};
        AppendInteger(deflate, length, 2);
        AppendInteger(deflate, static_cast<u16>(~length), 2);
        deflate.insert(deflate.end(), raw.begin(), raw.end());
        u32 adlerA = 1;
        u32 adlerB = 0;
        for (const auto byte : raw)
        {
            adlerA = (adlerA + byte) % 65521;
            adlerB = (adlerB + adlerA) % 65521;
        }
        AppendBigEndian(deflate, (adlerB << 16) | adlerA);
        std::vector<u8> header;
        AppendBigEndian(header, 2);
        AppendBigEndian(header, 2);
        constexpr std::array<u8, 4> COLOR_TYPES = {0, 4, 2, 6};
        header.insert(header.end(), {8, COLOR_TYPES[channels - 1], 0, 0, 0});
        std::vector<u8> png = {137, 80, 78, 71, 13, 10, 26, 10};
        AppendPngChunk(png, {'I', 'H', 'D', 'R'}, header);
        AppendPngChunk(png, {'I', 'D', 'A', 'T'}, deflate);
        AppendPngChunk(png, {'I', 'E', 'N', 'D'}, {});
        return png;
    }
}
