#pragma once
#include "Modules/Render/Shaders/Shader.h"

namespace rei::render
{
    class Material
    {
    public:
        REI_API Material() = default;
        REI_API Material(const Material&);
        REI_API explicit Material(assets::AssetRef<Shader> shader);

        REI_API ~Material();

        REI_API const Shader& GetShader() const;

    private:
        assets::AssetRef<Shader> _shader;
    };
}
