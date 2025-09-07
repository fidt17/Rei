#include "pch.h"
#include <regex>
#include "AssetBuilder.h"

#include <sstream>

#include "Serialization/BinaryWriter.h"

#include "Builders/ModelBuilder.h"
#include "Builders/TextureBuilder.h"
#include "Common/Time/ScopedTimer.h"

namespace rei::resources
{
    SET_LOG_SCOPE("Asset Builder")
    
    i64 AssetBuilder::BuildAsset(const std::string& file, const std::string& dest, const i64 offset) const
    {
        try
        {
            const std::filesystem::path assetPath = file;
            REI_THROW_IF(!std::filesystem::exists(assetPath), "File " + file + " does not exist")

            if (!std::filesystem::exists(dest))
            {
                std::ofstream outfile(dest);
                outfile.close();
            }

            BinaryWriter writer(dest, offset);
            const auto totalBytesWritten = Build(assetPath, writer);
            writer.Close();

            return totalBytesWritten;
        }
        catch (const std::exception& e)
        {
            LOG_ERROR("{}", e.what())
        }

        return 0;
    }

    i64 AssetBuilder::Build(const std::filesystem::path& filePath, BinaryWriter& writer) const
    {
        time::ScopedTimer timer("Asset " + filePath.string() + " building");
        
        const i64 offset = writer.GetPosition();

        LOG("Building asset: {}", filePath.string())

        #define ADD_TO_MAP(x, y) map[x] = [&](const std::filesystem::path& p, BinaryWriter& w) { y(p, w); };
        std::map<std::string, std::function<void(const std::filesystem::path&, BinaryWriter&)>> map;
        ADD_TO_MAP(".png", TextureBuilder().BuildTextureAsset)
        ADD_TO_MAP(".jpg", TextureBuilder().BuildTextureAsset)
        ADD_TO_MAP(".obj", ModelBuilder().BuildModelAsset)
        ADD_TO_MAP(".fbx", ModelBuilder().BuildModelAsset)

        const auto extension = filePath.extension().string();
        
        if (map.find(extension) == map.end())
        {
            BuildDataAsset(filePath, writer);
        }
        else
        {
            map[extension](filePath, writer);
        }

        const i64 bytesWritten = writer.GetPosition() - offset;
        LOG("Total Size: {} bytes", bytesWritten)

        return writer.GetPosition();
    }

    void AssetBuilder::EraseBOM(std::string& str) const
    {
        if (str[0] == -17 && str[1] == -69 && str[2] == -65)
        {
            str.erase(0, 3);
        }
    }

    std::string AssetBuilder::ReadAllText(const std::filesystem::path& path) const
    {
        REI_ASSERT(std::filesystem::exists(path), std::format("File {} does not exist", std::string(path)))

        std::stringstream strStream;
        strStream << std::ifstream(path).rdbuf();

        auto str = strStream.str();
        EraseBOM(str);

        return str;
    }

    void AssetBuilder::BuildDataAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const
    {
        const std::string str = ReadAllText(assetPath);
        writer.WriteStr(str);
    }
}
