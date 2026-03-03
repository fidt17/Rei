#pragma once

#include <vector>

namespace rei::render
{
    enum TextureType
    {
        Diffuse,
        Specular,
        Normal,
        Height
    };
    
    class Texture
    {
    public:
        REI_API explicit Texture(resources::BinaryReader& reader);
        REI_API void PostLoad();

        REI_API void Use(i32 idx = 0) const;

        REI_API u32 GetId() const;
        
        REI_API TextureType GetType() const;
        REI_API void SetType(TextureType type);

        REI_API std::string GetTag() const;

    private:
        u32 _id = 0;
        i32 _width = 0;
        i32 _height = 0;
        i32 _format = 0;
        std::vector<u8> _rawData{};
        std::string _textureTag;
        TextureType _type = Diffuse;
    };
}
