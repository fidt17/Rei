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
        explicit Texture(resources::BinaryReader& reader);
        REI_API void PostLoad();

        void Use(i32 idx = 0) const;

        u32 GetId() const;
        
        TextureType GetType() const;
        void SetType(TextureType type);

        std::string GetTag() const;

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
