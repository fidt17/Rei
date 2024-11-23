#pragma once

namespace rei::render
{
    class Texture
    {
    public:
        explicit Texture(const char* path, std::string type);
        explicit Texture(resources::BinaryReader& reader);

        void Use() const;

        u32 GetId() const;
        std::string GetType() const;

        std::string GetTag() const;

    private:
        u32 _id;
        std::string _textureTag;
        std::string _type = "texture_diffuse";
    };
}
