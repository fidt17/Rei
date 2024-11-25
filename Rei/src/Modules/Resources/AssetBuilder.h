#pragma once
#include "Serialization/BinaryWriter.h"

namespace rei::resources
{
    class AssetBuilder
    {
    public:
        REI_API i64 BuildAsset(const std::string& file, const std::string& dest, const i64 offset) const;
        REI_API i64 Build(const std::filesystem::path& filePath, BinaryWriter& writer) const;
        
    private:
        void EraseBOM(std::string& str) const;

        std::string ReadAllText(const std::filesystem::path& path) const;

        void BuildDataAsset(const std::filesystem::path& assetPath, BinaryWriter& writer) const;
    };
}
