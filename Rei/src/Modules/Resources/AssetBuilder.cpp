#include "pch.h"
#include <regex>
#include "AssetBuilder.h"

#include <sstream>

#include "Serialization/BinaryWriter.h"

namespace rei::resources
{
    enum AssetType
    {
        Data = 0,
        Scene = 1,
    };

    std::string ReadAllText(const std::filesystem::path& path)
    {
        REI_ASSERT(std::filesystem::exists(path), "File " + path.string() + " does not exist")

        std::stringstream strStream;
        strStream << std::ifstream(path).rdbuf();

        return strStream.str();
    }

    void BuildDataAsset(const std::filesystem::path& assetPath, BinaryWriter& writer)
    {
        const std::string str = ReadAllText(assetPath);
        writer.WriteStr(str);
    }

    i64 Build(const AssetType assetType, const std::filesystem::path& filePath, BinaryWriter& writer)
    {
        BuildDataAsset(filePath, writer);
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
                std::ofstream outfile(dest);
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
