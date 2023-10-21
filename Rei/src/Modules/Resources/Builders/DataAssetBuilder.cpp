#include "pch.h"
#include "DataAssetBuilder.h"

#include <sstream>

namespace rei::resources
{
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
}
