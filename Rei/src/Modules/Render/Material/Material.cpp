#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Material.h"

#include "glad/glad.h"
#include <type_traits>

namespace rei::render
{
    Material::Material(resources::BinaryReader& reader)
    {
        try
        {
            const auto rawData = reader.GetStr();
            const auto data = nlohmann::json::parse(rawData);

            if (!data.contains("ShaderAssetId") || !data.at("ShaderAssetId").is_string())
            {
                LOG_ERROR("Material asset is missing valid 'ShaderAssetId'.")
                return;
            }

            const auto shaderAssetId = data.at("ShaderAssetId").get<std::string>();
            if (shaderAssetId.empty())
            {
                LOG_ERROR("Material asset has empty 'ShaderAssetId'.")
                return;
            }

            _shader = GetAssetManager().GetById<Shader>(shaderAssetId);
            if (!_shader.IsLoaded())
            {
                LOG_ERROR("Failed to load shader '{}' for material.", shaderAssetId)
            }

            LoadSerializableFields(data);
        }
        catch (const std::exception& e)
        {
            LOG_ERROR("Failed to parse material asset. Error: {}", e.what())
        }
    }

    Material::Material(const assets::AssetRef<Shader>& shader)
        : _shader(shader)
    {
    }

    nlohmann::json Material::REI_GET() const
    {
        std::vector<std::pair<std::string, nlohmann::json>> orderedProperties;
        orderedProperties.reserve(_properties.size());
        for (const auto& [key, value] : _properties)
        {
            orderedProperties.emplace_back(key, value);
        }
        std::sort(orderedProperties.begin(), orderedProperties.end(), [](const auto& lhs, const auto& rhs)
        {
            return lhs.first < rhs.first;
        });

        auto properties = nlohmann::json::object();
        for (const auto& [key, value] : orderedProperties)
        {
            properties[key] = value;
        }

        nlohmann::json data;
        data["ShaderAssetId"] = _shader.Id;
        data["UseDepth"] = _useDepth;
        data["SortingOrder"] = _sortingOrder;
        data["Properties"] = properties;
        return data;
    }

    void Material::REI_SET(const nlohmann::json& data)
    {
        LoadSerializableFields(data);
    }

    void Material::Use() const
    {
        REI_PROFILE_SCOPE(profiling::markers::MATERIAL.Id);
        if (!_shader.IsLoaded())
        {
            LOG_ERROR("Shader {} is not loaded. Cannot use this material", _shader.Id);
            return;
        }

        profiling::Count(profiling::markers::MATERIAL_BINDS.Id);
        _shader->Use();
        profiling::UniformPhaseScope phase(profiling::UniformPhase::Material);
        SyncShaderBindings();

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

    const assets::AssetRef<Shader>& Material::GetShaderAsset() const
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

    i32 Material::GetInt(const std::string& name, const i32 fallback) const
    {
        const auto property = _properties.find(name);
        if (property == _properties.end() || !property->second.is_number()) return fallback;
        return property->second.get<i32>();
    }

    f32 Material::GetFloat(const std::string& name, const f32 fallback) const
    {
        const auto property = _properties.find(name);
        if (property == _properties.end() || !property->second.is_number()) return fallback;
        return property->second.get<f32>();
    }

    void Material::SetInt(const std::string& name, const i32 value)
    {
        profiling::Count(profiling::markers::PROPERTY_WRITES.Id);
        if (name.empty()) return;
        _properties[name] = value;
        UpdatePropertyBinding(name, _properties.at(name));
    }

    void Material::SetFloat(const std::string& name, const f32 value)
    {
        profiling::Count(profiling::markers::PROPERTY_WRITES.Id);
        if (name.empty()) return;
        _properties[name] = value;
        UpdatePropertyBinding(name, _properties.at(name));
    }

    void Material::SetColor(const std::string& name, const Color& value)
    {
        profiling::Count(profiling::markers::PROPERTY_WRITES.Id);
        if (name.empty()) return;

        _properties[name] = nlohmann::json::object({
            {"r", value.r},
            {"g", value.g},
            {"b", value.b},
            {"a", value.a}
        });
        UpdatePropertyBinding(name, _properties.at(name));
    }

    void Material::SetTexture(const std::string& name, const assets::AssetRef<Texture>& texture)
    {
        profiling::Count(profiling::markers::PROPERTY_WRITES.Id);
        if (name.empty()) return;

        _properties[name] = nlohmann::json::object({
            {"Id", texture.Id}
        });
        UpdatePropertyBinding(name, _properties.at(name));
        std::get<TextureBinding>(_propertyBindings.at(name)).Asset.Record = texture.Record;
    }

    void Material::ClearProperty(const std::string& name)
    {
        if (name.empty()) return;
        _properties.erase(name);
        _propertyBindings.erase(name);
    }

    assets::AssetRef<Material> Material::CreateInstanceFrom(const Material& source)
    {
        auto material = GetAssetManager().CreateAsset<Material>(GetAssetManager().CreateAsset<Shader>(Shader::CreateInstanceFrom(*source._shader.Get())));
        material->_useDepth = source._useDepth;
        material->_sortingOrder = source._sortingOrder;
        material->_textures = source._textures;
        material->_properties = source._properties;
        material->_propertyBindings = source._propertyBindings;

        return material;
    }

    void Material::SyncShaderBindings() const
    {
        if (!_shader.IsLoaded()) return;

        auto boundTextureUniforms = BindTextures();
        auto textureSlot = static_cast<i32>(_textures.size());
        ApplyShaderProperties(boundTextureUniforms, textureSlot);
        BindMissingTextureUniforms(boundTextureUniforms, textureSlot);
    }

    const assets::AssetRef<Texture>& Material::GetWhiteFallbackTexture() const
    {
        if (!_whiteFallback.IsLoaded())
        {
            _whiteFallback.Id = REI_WHITE_FALLBACK_TEXTURE_ID;
            _whiteFallback.Record = GetAssetManager().GetById<Texture>(_whiteFallback.Id).Record;
        }
        if (!_whiteFallback.IsLoaded()) LOG_ERROR("White fallback texture '{}' is not loaded.", REI_WHITE_FALLBACK_TEXTURE_ID)
        return _whiteFallback;
    }

    std::unordered_set<std::string> Material::BindTextures() const
    {
        std::unordered_set<std::string> boundTextureUniforms;
        u32 diffuseNr = 1;
        u32 specularNr = 1;
        u32 normalNr = 1;
        u32 heightNr = 1;

        for (u32 i = 0; i < _textures.size(); i++)
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
                LOG_ERROR("Unknown texture type: {}", static_cast<i32>(textureType))
                continue;
            }

            const auto uniformName = textureName + number;
            _shader->SetInt(uniformName, i);
            texturePtr->Use(i);
            boundTextureUniforms.insert(uniformName);
        }

        return boundTextureUniforms;
    }

    void Material::ApplyShaderProperties(std::unordered_set<std::string>& boundTextureUniforms, i32& textureSlot) const
    {
        for (const auto& [uniformName, binding] : _propertyBindings)
        {
            if (_shader->GetLocation(uniformName) < 0) continue;
            std::visit([&](const auto& value)
            {
                using T = std::decay_t<decltype(value)>;
                if constexpr (std::is_same_v<T, i32>)
                {
                    _shader->SetInt(uniformName, value);
                }
                else if constexpr (std::is_same_v<T, f32>)
                {
                    _shader->SetFloat(uniformName, value);
                }
                else if constexpr (std::is_same_v<T, Color>)
                {
                    _shader->SetColor(uniformName, value);
                }
                else if constexpr (std::is_same_v<T, TextureBinding>)
                {
                    auto& texture = value.Asset;
                    if (!texture.Id.empty() && !texture.IsLoaded()) texture.Record = GetAssetManager().GetById<Texture>(texture.Id).Record;
                    const auto& resolved = texture.IsLoaded() ? texture : GetWhiteFallbackTexture();
                    if (!resolved.IsLoaded()) return;
                    _shader->SetInt(uniformName, textureSlot);
                    resolved->Use(textureSlot++);
                    boundTextureUniforms.insert(uniformName);
                }
            }, binding);
        }
    }

    void Material::BindMissingTextureUniforms(const std::unordered_set<std::string>& boundTextureUniforms, i32& textureSlot) const
    {
        const auto& samplerUniformNames = _shader->GetUniformNamesByType(GL_SAMPLER_2D);
        for (const auto& uniformName : samplerUniformNames)
        {
            if (uniformName.empty() || boundTextureUniforms.contains(uniformName)) continue;
            const auto& fallbackTexture = GetWhiteFallbackTexture();
            if (!fallbackTexture.IsLoaded()) return;
            _shader->SetInt(uniformName, textureSlot);
            fallbackTexture->Use(textureSlot++);
        }
    }

    void Material::UpdatePropertyBinding(const std::string& name, const nlohmann::json& value)
    {
        f32 floatValue = 0;
        i32 intValue = 0;
        bool isInteger = false;
        Color color = Color::White();
        std::string textureId;
        if (TryReadNumber(value, floatValue, intValue, isInteger))
        {
            if (isInteger) _propertyBindings[name] = intValue;
            else _propertyBindings[name] = floatValue;
        }
        else if (TryReadColor(value, color))
        {
            _propertyBindings[name] = color;
        }
        else if (TryReadTextureAssetId(value, textureId))
        {
            auto& binding = _propertyBindings[name];
            auto* textureBinding = std::get_if<TextureBinding>(&binding);
            if (!textureBinding) textureBinding = &binding.emplace<TextureBinding>();
            auto& texture = textureBinding->Asset;
            if (texture.Id != textureId)
            {
                texture.Id = std::move(textureId);
                texture.Record.reset();
            }
        }
        else
        {
            _propertyBindings.erase(name);
        }
    }

    void Material::LoadSerializableFields(const nlohmann::json& data)
    {
        if (data.contains("ShaderAssetId") && data.at("ShaderAssetId").is_string())
        {
            const auto shaderAssetId = data.at("ShaderAssetId").get<std::string>();
            _shader = shaderAssetId.empty()
                ? assets::AssetRef<Shader>()
                : GetAssetManager().GetById<Shader>(shaderAssetId);
        }

        if (data.contains("UseDepth") && data.at("UseDepth").is_boolean())
        {
            _useDepth = data.at("UseDepth").get<bool>();
        }

        if (data.contains("SortingOrder") && data.at("SortingOrder").is_number_integer())
        {
            _sortingOrder = data.at("SortingOrder").get<i32>();
        }

        if (!data.contains("Properties") || !data.at("Properties").is_object()) return;

        _properties.clear();
        _propertyBindings.clear();
        for (const auto& [uniformName, value] : data.at("Properties").items())
        {
            if (uniformName.empty()) continue;
            _properties[uniformName] = value;
            UpdatePropertyBinding(uniformName, value);
        }
    }

    bool Material::TryReadNumber(const nlohmann::json& value, f32& outFloatValue, i32& outIntValue, bool& isInteger)
    {
        isInteger = false;

        if (value.is_number_integer())
        {
            outIntValue = value.get<i32>();
            isInteger = true;
            return true;
        }

        if (value.is_number_unsigned())
        {
            outIntValue = static_cast<i32>(value.get<u32>());
            isInteger = true;
            return true;
        }

        if (!value.is_number_float()) return false;

        outFloatValue = value.get<f32>();
        return true;
    }

    bool Material::TryReadColor(const nlohmann::json& value, Color& outColor)
    {
        if (!value.is_object()) return false;
        if (!value.contains("r") || !value.contains("g") || !value.contains("b")) return false;

        if (!value.at("r").is_number() || !value.at("g").is_number() || !value.at("b").is_number()) return false;
        if (value.contains("a") && !value.at("a").is_number()) return false;

        outColor.r = value.at("r").get<f32>();
        outColor.g = value.at("g").get<f32>();
        outColor.b = value.at("b").get<f32>();
        outColor.a = value.contains("a") ? value.at("a").get<f32>() : 1.0f;

        return true;
    }

    bool Material::TryReadTextureAssetId(const nlohmann::json& value, std::string& outTextureAssetId)
    {
        if (!value.is_object()) return false;
        if (!value.contains("Id") || !value.at("Id").is_string()) return false;

        outTextureAssetId = value.at("Id").get<std::string>();
        return true;
    }
}
