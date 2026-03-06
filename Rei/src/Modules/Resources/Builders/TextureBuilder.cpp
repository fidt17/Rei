#include "pch.h"
#include "TextureBuilder.h"

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"
#include "glad/glad.h"

void rei::resources::TextureBuilder::BuildTextureAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const
{
    stbi_set_flip_vertically_on_load(true);
    
    i32 width, height, nrChannels;
    unsigned char* data = stbi_load(assetPath.string().c_str(), &width, &height, &nrChannels, 0);
    GLenum format;
    if (nrChannels == 1)
    {
        format = GL_RED;
    }
    else if (nrChannels == 3)
    {
        format = GL_RGB;
    }
    else if (nrChannels == 4)
    {
        format = GL_RGBA;
    }

    writer.WriteI32(width);
    writer.WriteI32(height);
    writer.WriteI32(format);

    const i32 length = width * height * nrChannels;
    writer.WriteBytes(data, length);

    stbi_image_free(data);

    LOG("Width: {}", width)
    LOG("Height: {}", height)
    LOG("Number of channels: {}", nrChannels)

    if (format == GL_RGB)
    {
        LOG("Format: RGB");
    }
    else
    {
        LOG("Format: RGBA");
    }

    LOG("Length: {}", length)
}
