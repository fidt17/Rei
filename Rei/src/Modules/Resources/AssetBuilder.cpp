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

    i64 Build(const std::filesystem::path& filePath, BinaryWriter& writer)
    {
        BuildDataAsset(filePath, writer);
        return writer.GetPosition();
    }

    i64 BuildAsset(const std::string& file, const std::string& dest, const i64 offset)
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
            const auto bytesWritten = Build(assetPath, writer);
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
