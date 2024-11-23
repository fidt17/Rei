#include "pch.h"
#include "Texture.h"

#include "stb_image.h"
#include "glad/glad.h"

rei::render::Texture::Texture(const char* path, std::string type)
{
    stbi_set_flip_vertically_on_load(true);
    
    _textureTag = path;
    _type = type;
    
    glGenTextures(1, &_id);

    int width, height, nrComponents;
    unsigned char* data = stbi_load(path, &width, &height, &nrComponents, 0);
    if (data)
    {
        GLenum format;
        if (nrComponents == 1)
            format = GL_RED;
        else if (nrComponents == 3)
            format = GL_RGB;
        else if (nrComponents == 4)
            format = GL_RGBA;

        glBindTexture(GL_TEXTURE_2D, _id);
        glTexImage2D(GL_TEXTURE_2D, 0, format, width, height, 0, format, GL_UNSIGNED_BYTE, data);
        glGenerateMipmap(GL_TEXTURE_2D);

        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

        stbi_image_free(data);
    }
    else
    {
        std::cout << "Texture failed to load at path: " << path << std::endl;
        stbi_image_free(data);
    }
}

rei::render::Texture::Texture(resources::BinaryReader& reader)
{
    i32 width = reader.GetI32();
    i32 height = reader.GetI32();
    i32 mode = reader.GetI32();

    i32 length;
    unsigned char* data = reader.GetBytes(length);

    glGenTextures(1, &_id);
    glBindTexture(GL_TEXTURE_2D, _id);

    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

    glTexImage2D(GL_TEXTURE_2D, 0, mode, width, height, 0, mode, GL_UNSIGNED_BYTE, data);
    glGenerateMipmap(GL_TEXTURE_2D);
}

void rei::render::Texture::Use() const
{
    glBindTexture(GL_TEXTURE_2D, _id);
}

u32 rei::render::Texture::GetId() const
{
    return _id;
}

std::string rei::render::Texture::GetType() const
{
    return _type;
}

std::string rei::render::Texture::GetTag() const
{
    return _textureTag;
}
