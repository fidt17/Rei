#include "pch.h"
#include "Texture2D.h"

#include "glad/glad.h"

namespace rei::render
{
    Texture2D::Texture2D(resources::BinaryReader& reader)
    {
        const i32 width = reader.GetI32();
        const i32 height = reader.GetI32();
        const i32 mode = reader.GetI32();
        
        i32 length;
        u8* data = reader.GetBytes(length);

        glGenTextures(1, &_id);
        glBindTexture(GL_TEXTURE_2D, _id);

        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);	
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        
        glTexImage2D(GL_TEXTURE_2D, 0, mode, width, height, 0, mode, GL_UNSIGNED_BYTE, data);
        glGenerateMipmap(GL_TEXTURE_2D);

        delete data;
    }

    void Texture2D::Use() const
    {
        glBindTexture(GL_TEXTURE_2D, _id);
    }
}
