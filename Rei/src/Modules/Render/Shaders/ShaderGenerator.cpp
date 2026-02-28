#include "pch.h"
#include "ShaderGenerator.h"

#include "Modules/Assets/Core/AssetIds.h"
#include "Modules/Assets/Types/TextAsset.h"
#include <vector>

namespace rei::render
{
    namespace
    {
        std::string BuildIncludeBlock(const std::string& blockName, const std::vector<std::string>& includeAssetIds)
        {
            std::string block = "\n// --- SHADER " + blockName + " INCLUDES START ---\n";

            for (const auto& includeAssetId : includeAssetIds)
            {
                const auto includeAsset = GetAssetManager().GetById<assets::TextAsset>(includeAssetId);
                block += "\n" + includeAsset->GetValue();
            }

            block += "\n// --- SHADER " + blockName + " INCLUDES END ---\n";
            return block;
        }
    }

    ShaderGenerator& ShaderGenerator::GetInstance()
    {
        static ShaderGenerator instance;
        return instance;
    }

    void ShaderGenerator::Initialize()
    {
        if (_isInitialized) return;

        _commonIncludes = BuildIncludeBlock("COMMON",
        {
            REI_SHADER_INCLUDE_AMBIENT_LIGHT_ASSET_ID,
            REI_SHADER_INCLUDE_POINT_LIGHT_ASSET_ID,
            REI_SHADER_INCLUDE_SHADER_COMMON_ASSET_ID
        });
        _commonIncludes = "\n#define NR_POINT_LIGHTS " + STRING(REI_MAX_POINT_LIGHTS_COUNT) + "\n" + _commonIncludes;

        _vertexIncludes = BuildIncludeBlock("VERTEX", {REI_SHADER_INCLUDE_VERTEX_COMMON_ASSET_ID});
        _fragmentIncludes = BuildIncludeBlock("FRAGMENT", {REI_SHADER_INCLUDE_FRAGMENT_COMMON_ASSET_ID});
        _isInitialized = true;
    }

    std::string ShaderGenerator::ComposeVertexSource(const std::string& content) const
    {
        REI_THROW_IF(!_isInitialized, "ShaderGenerator is not initialized");
        return "#version 330 core\n" + _commonIncludes + _vertexIncludes + "\n#define VERTEX;\n" + content;
    }

    std::string ShaderGenerator::ComposeFragmentSource(const std::string& content) const
    {
        REI_THROW_IF(!_isInitialized, "ShaderGenerator is not initialized");
        return "#version 330 core\n" + _commonIncludes + _fragmentIncludes + "\n#define FRAGMENT;\n" + content;
    }
}
