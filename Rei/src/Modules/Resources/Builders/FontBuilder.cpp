#include "pch.h"
#include "FontBuilder.h"

#include "Modules/Resources/Serialization/BinaryWriter.h"

void rei::resources::FontBuilder::BuildFontAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const
{
    REI_THROW_IF(!std::filesystem::exists(assetPath), "Font file does not exist: " + assetPath.string())

    std::ifstream stream(assetPath, std::ios::binary | std::ios::ate);
    REI_THROW_IF(!stream.is_open(), "Could not open font file: " + assetPath.string())

    const auto length = static_cast<i32>(stream.tellg());
    REI_THROW_IF(length <= 0, "Font file is empty: " + assetPath.string())

    std::vector<u8> data(length);
    stream.seekg(0);
    stream.read(reinterpret_cast<char*>(data.data()), length);

    writer.WriteBytes(data.data(), length);
    LOG("Font bytes: {}", length)
}
