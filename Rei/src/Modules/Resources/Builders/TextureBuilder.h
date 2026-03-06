#pragma once

namespace rei::resources
{
    class TextureBuilder
    {
    public:
        void BuildTextureAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const;
    };
}
