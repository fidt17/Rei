#include "pch.h"
#include "Texture.h"

#include "stb_image.h"
#include "glad/glad.h"

rei::render::Texture::Texture(resources::BinaryReader& reader)
{
    _width = reader.GetI32();
    _height = reader.GetI32();
    _format = reader.GetI32();

    i32 length = 0;
    u8* data = reader.GetBytes(length);
    _rawData = std::vector<u8>(data, data + length);
    delete[] data;
}

void rei::render::Texture::PostLoad()
{
    if (_id != 0)
    {
        return;
    }

    glGenTextures(1, &_id);
    glBindTexture(GL_TEXTURE_2D, _id);

    glTexImage2D(GL_TEXTURE_2D, 0, _format, _width, _height, 0, _format, GL_UNSIGNED_BYTE, _rawData.data());
    glGenerateMipmap(GL_TEXTURE_2D);

    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

    _rawData.clear();
    _rawData.shrink_to_fit();
}

void rei::render::Texture::Use(const i32 idx) const
{
    glActiveTexture(GL_TEXTURE0 + idx);
    glBindTexture(GL_TEXTURE_2D, _id);
}

u32 rei::render::Texture::GetId() const
{
    return _id;
}

rei::render::TextureType rei::render::Texture::GetType() const
{
    return _type;
}

void rei::render::Texture::SetType(const TextureType type)
{
    _type = type;
}

std::string rei::render::Texture::GetTag() const
{
    return _textureTag;
}
