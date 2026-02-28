#include "pch.h"
#include "Material.h"

#include "glad/glad.h"

namespace rei::render
{

    Material::Material(resources::BinaryReader& reader)
    {
        auto assignFallbackShader = [this]()
        {
            _shader = GetAssetManager().GetById<Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        };

        try
        {
            const auto rawData = reader.GetStr();
            const auto data = nlohmann::json::parse(rawData);

            if (!data.contains("ShaderAssetId") || !data.at("ShaderAssetId").is_string())
            {
                LOG_ERROR("Material asset is missing valid 'ShaderAssetId'. Falling back to {}", REI_SHADER_SIMPLE_LIT_ASSET_ID)
                assignFallbackShader();
                return;
            }

            const auto shaderAssetId = data.at("ShaderAssetId").get<std::string>();
            if (shaderAssetId.empty())
            {
                LOG_ERROR("Material asset has empty 'ShaderAssetId'. Falling back to {}", REI_SHADER_SIMPLE_LIT_ASSET_ID)
                assignFallbackShader();
                return;
            }

            _shader = GetAssetManager().GetById<Shader>(shaderAssetId);
            if (!_shader.IsLoaded())
            {
                LOG_ERROR("Failed to load shader '{}' for material. Falling back to {}", shaderAssetId, REI_SHADER_SIMPLE_LIT_ASSET_ID)
                assignFallbackShader();
            }
        }
        catch (const std::exception& e)
        {
            LOG_ERROR("Failed to parse material asset. Falling back to {}. Error: {}", REI_SHADER_SIMPLE_LIT_ASSET_ID, e.what())
            assignFallbackShader();
        }
        
        LOG_DEBUG("Created material: {}", _shader.Id);
    }

    Material::Material(const assets::AssetRef<Shader>& shader)
        : _shader(shader)
    {
        LOG_DEBUG("Created material: {}", _shader.Id);
    }

    Material::~Material()
    {
        LOG_DEBUG("Deleting material: {}", _shader.Id);
        GetAssetManager().Release(_shader);
    }

    void Material::Use() const
    {
        if (!_shader.IsLoaded())
        {
            LOG_ERROR("Shader {} is not loaded. Cannot use this material", _shader.Id);
            return;
        }

        _shader->Use();
        BindTextures();

        if (UseDepth())
        {
            glEnable(GL_DEPTH_TEST);
        }
        else
        {
            glDisable(GL_DEPTH_TEST);
        }
    }

    assets::AssetRef<Shader>& Material::GetShaderAsset()
    {
        return _shader;
    }

    const Shader& Material::GetShader() const
    {
        return *_shader.Get();
    }

    std::vector<assets::AssetRef<Texture>>& Material::GetTextures()
    {
        return _textures;
    }

    bool Material::UseDepth() const
    {
        return _useDepth;
    }

    void Material::SetDepth(const bool value)
    {
        _useDepth = value;
    }

    assets::AssetRef<Material> Material::CreateInstanceFrom(const Material& source)
    {
        auto material = GetAssetManager().CreateAsset<Material>(GetAssetManager().CreateAsset<Shader>(Shader::CreateInstanceFrom(*source._shader.Get())));
        material->_useDepth = source._useDepth;
        material->_sortingOrder = source._sortingOrder;
        material->_textures = source._textures;
        
        LOG_DEBUG("Created material instance id={}, shader={}", material.Id, material->_shader.Id)

        return material;
    }

    void Material::BindTextures() const
    {
        unsigned int diffuseNr = 1;
        unsigned int specularNr = 1;
        unsigned int normalNr = 1;
        unsigned int heightNr = 1;

        for (unsigned int i = 0; i < _textures.size(); i++)
        {
            if (!_textures[i].IsLoaded())
            {
                LOG_ERROR("Texture {} is not loaded", _textures[i].Id)
                continue;
            }
            const auto texturePtr = _textures[i].Get();

            std::string number;
            std::string textureName;
            switch (const TextureType textureType = texturePtr->GetType())
            {
            case Diffuse:
                number = std::to_string(diffuseNr++);
                textureName = "texture_diffuse";
                break;
            case Specular:
                number = std::to_string(specularNr++);
                textureName = "texture_specular";
                break;
            case Normal:
                number = std::to_string(normalNr++);
                textureName = "texture_normal";
                break;
            case Height:
                number = std::to_string(heightNr++);
                textureName = "texture_height";
                break;
            default:
                LOG_ERROR("Unknown texture type: {}", static_cast<int>(textureType))
                continue;
            }

            _shader->SetInt(textureName + number, i);
            texturePtr->Use(i);
        }
    }
}



