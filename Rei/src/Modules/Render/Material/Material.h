#pragma once
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture.h"

namespace rei::render
{
    class Material
    {
    public:
        REI_API Material() = default;
        REI_API Material(resources::BinaryReader& reader);
        REI_API Material(const assets::AssetRef<Shader>& shader);
        REI_API nlohmann::json REI_GET() const;
        REI_API void REI_SET(const nlohmann::json& data);

        REI_API void Use() const;

        REI_API assets::AssetRef<Shader>& GetShaderAsset();
        REI_API const Shader& GetShader() const;
        REI_API std::vector<assets::AssetRef<Texture>>& GetTextures();

        REI_API bool UseDepth() const;
        REI_API void SetDepth(bool value);

        REI_API void SetSortingOrder(const i32 value) { _sortingOrder = value; }
        REI_API i32 GetSortingOrder() const { return _sortingOrder; }

        static REI_API assets::AssetRef<Material> CreateInstanceFrom(const Material& source);

    private:
        void BindTextures() const;

    private:
        assets::AssetRef<Shader> _shader;
        std::vector<assets::AssetRef<Texture>> _textures = {};

        bool _useDepth = true;
        i32 _sortingOrder = SORTING_ORDER_DEFAULT;
    };
}
