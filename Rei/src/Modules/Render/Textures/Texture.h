#pragma once

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

        void Use() const;

        u32 GetId() const;
        
        TextureType GetType() const;
        void SetType(TextureType type);

        std::string GetTag() const;

    private:
        u32 _id;
        std::string _textureTag;
        TextureType _type = Diffuse;
    };
}
