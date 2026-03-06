#include "pch.h"
#include "AssetsMap.h"

#include <cstdlib>
#include <vector>

#include "Modules/Resources/Serialization/BinaryReader.h"

namespace rei::assets
{
    void AssetsMap::Initialize()
    {
        REI_THROW_IF(_initialized, "Assets map is already initialized")
        
        const std::filesystem::path currentPath = std::filesystem::current_path();

        std::vector<std::filesystem::path> checkPaths;
        char* resourcesPathOverride = std::getenv("REI_RESOURCES_PATH");
        if (resourcesPathOverride != nullptr && resourcesPathOverride[0] != '\0')
        {
            checkPaths.emplace_back(resourcesPathOverride);
        }

        checkPaths.push_back(currentPath);
        checkPaths.push_back(currentPath / "Resources");
        checkPaths.push_back(currentPath / ".." / "Resources");

        std::filesystem::path resourcesPath;
        for (const auto& checkPath : checkPaths)
        {
            LOG_DEBUG("Checking resources path: {}", checkPath.string())
            const auto mapPath = checkPath / "map.bin";
            if (!std::filesystem::exists(mapPath))
            {
                continue;
            }

            resourcesPath = checkPath;
            std::filesystem::current_path(resourcesPath);
            LOG_DEBUG("Resources path found: {}", resourcesPath.string())
            break;
        }

        REI_THROW_IF(resourcesPath.empty(), "Resources folder is missing")

        auto reader = resources::BinaryReader("map.bin", 0);
        const i32 count = reader.GetI32();

        for (auto i = 0; i < count; i++)
        {
            const auto id = reader.GetStr();
            const auto assetName = reader.GetStr();
            const auto path = reader.GetStr();
            const i64 offset = reader.GetI64();

            _assets.insert({id, BuildAssetInfo(path, offset, assetName)});
        }
        reader.Close();
        
        _initialized = true;
        
        LOG("Loaded assets map from {}", resourcesPath.string())
    }

    BuildAssetInfo AssetsMap::GetAssetInfo(const std::string& id) const
    {
        REI_THROW_IF(!_initialized, "Assets map is not initialized")
        REI_THROW_IF(!_assets.contains(id), "Missing asset with id: " + id)

        return _assets.at(id);
    }
}
