#pragma once
#include "Modules/Render/Textures/Texture.h"

namespace rei::resources
{
    class TextureBuilder
    {
    public:
        void BuildTextureAsset(const std::filesystem::path& assetPath, BinaryWriter& writer, render::TextureColorSpace colorSpace = render::TextureColorSpace::Srgb) const;
    };
}
