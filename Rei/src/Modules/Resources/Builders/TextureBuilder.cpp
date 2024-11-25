#include "pch.h"
#include "TextureBuilder.h"

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"
#include "glad/glad.h"

void rei::resources::TextureBuilder::BuildTextureAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const
{
    auto extension = assetPath.extension();

    i32 width, height, nrChannels;
    unsigned char* data = stbi_load(assetPath.string().c_str(), &width, &height, &nrChannels, 0);
    i32 mode = nrChannels == 4 ? GL_RGBA : GL_RGB;

    writer.WriteI32(width);
    writer.WriteI32(height);
    writer.WriteI32(mode);

    const i32 length = width * height * nrChannels;
    writer.WriteBytes(data, length);

    stbi_image_free(data);

    LOG("Width: " + STRING(width))
    LOG("Height: " + STRING(height))
    LOG("Number of channels: " + STRING(nrChannels))

    if (mode == GL_RGB)
    {
        LOG("Format: RGB");
    }
    else
    {
        LOG("Format: RGBA");
    }

    LOG("Length: " + STRING(length))
}
