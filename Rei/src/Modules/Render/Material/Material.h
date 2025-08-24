#pragma once
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture.h"

#define REI_ERROR_MATERIAL_ID "REI_ERROR_MATERIAL"

namespace rei::render
{
    class Material
    {
    public:
        REI_API Material() = default;
        REI_API Material(resources::BinaryReader& reader);
        REI_API explicit Material(assets::AssetRef<Shader> shader);

        REI_API ~Material();

        REI_API const Shader& GetShader() const;
        REI_API std::vector<assets::AssetRef<Texture>>& GetTextures();

    private:
        assets::AssetRef<Shader> _shader;
        std::vector<assets::AssetRef<Texture>> _textures = {};
    };
}
