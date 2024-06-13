#include "pch.h"
#include <regex>
#include "AssetBuilder.h"

#include <sstream>

#include "Serialization/BinaryWriter.h"

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"
#include "glad/glad.h"

namespace rei::resources
{
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
            LOG_ERROR(e.what())
        }

        return 0;
    }

    i64 AssetBuilder::Build(const std::filesystem::path& filePath, BinaryWriter& writer) const
    {
        const i64 offset = writer.GetPosition();

        LOG("Path: " + filePath.string())
        LOG("File name: " + filePath.filename().string())

        if (filePath.extension() == ".png")
        {
            BuildTextureAsset(filePath, writer);
        }
        else
        {
            BuildDataAsset(filePath, writer);
        }

        const i64 bytesWritten = writer.GetPosition() - offset;
        LOG("Total Size: " + STRING(bytesWritten) + " bytes\n")

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
        REI_ASSERT(std::filesystem::exists(path), "File " + path.string() + " does not exist")

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

    void AssetBuilder::BuildTextureAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const
    {
        auto extension = assetPath.extension();

        i32 width, height, nrChannels;
        unsigned char* data = stbi_load(assetPath.string().c_str(), &width, &height, &nrChannels, 0);
        i32 mode = nrChannels == 4 ? GL_RGBA : GL_RGB;

        writer.WriteI32(width);
        writer.WriteI32(height);
        writer.WriteI32(mode);

        const i32 length = width * height * nrChannels;
        writer.WriteBytes(data, length);

        stbi_image_free(data);

        LOG("Width: " + STRING(width))
        LOG("Height: " + STRING(height))
        LOG("Number of channels: " + STRING(nrChannels))

        if (mode == GL_RGB)
            LOG("Format: RGB")
        else
            LOG("Format: RGBA")

        LOG("Length: " + STRING(length))
    }
}
