#include "pch.h"
#include <regex>
#include "AssetBuilder.h"
#include "Serialization/BinaryWriter.h"
#include "Builders/DataAssetBuilder.h"
#include "Builders/SceneAssetBuilder.h"

namespace rei::resources
{
    enum AssetType
    {
        Data = 0,
        Scene = 1,
    };

    i64 Build(const AssetType assetType, const std::filesystem::path& filePath, BinaryWriter& writer)
    {
        switch (assetType)
        {
        case Data:
            BuildDataAsset(filePath, writer);
            break;
        case Scene:
            BuildSceneAsset(filePath, writer);
            break;
        default:
            REI_THROW("Not supported asset type")
        }

        return writer.GetPosition();
    }

    AssetType GetAssetType(const std::filesystem::path& assetPath)
    {
        const std::filesystem::path metaPath = std::regex_replace(assetPath.string(), std::regex(assetPath.extension().string()), ".meta");
        REI_THROW_IF(!std::filesystem::exists(metaPath), "MetaFile " + metaPath.string() + " does not exist")

        const nlohmann::json metaJson = nlohmann::json::parse(std::ifstream(metaPath.string()));
        return metaJson.at("Type");
    }

    i64 BuildAsset(const std::string& file, const std::string& dest, const i64 offset)
    {
        try
        {
            const std::filesystem::path assetPath = file;
            REI_THROW_IF(!std::filesystem::exists(assetPath), "File " + file + " does not exist")

            const AssetType assetType = GetAssetType(assetPath);

            if (!std::filesystem::exists(dest))
            {
                std::ofstream outfile (dest);
                outfile.close();
            }
            
            BinaryWriter writer(dest, offset);
            const auto bytesWritten = Build(assetType, assetPath, writer);
            writer.Close();

            return bytesWritten;
        }
        catch (const std::exception& e)
        {
            LOG_ERROR(e.what())
        }

        return 0;
    }
}
