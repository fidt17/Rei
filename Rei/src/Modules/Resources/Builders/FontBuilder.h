#pragma once

namespace rei::resources
{
    class BinaryWriter;

    class FontBuilder
    {
    public:
        void BuildFontAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const;
    };
}
