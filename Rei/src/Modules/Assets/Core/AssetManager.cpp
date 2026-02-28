#include "pch.h"
#include "AssetManager.h"

#include "AssetLoadUtils.h"
#include "Engine/Services.h"
#include <cstdlib>

namespace rei::assets
{
    AssetManager::AssetManager()
    {
        const std::string currentPath = std::filesystem::current_path().string();
        
        std::vector<std::string> checkPaths;
        char * resourcesPathOverride = getenv( "REI_RESOURCES_PATH" );
        if (resourcesPathOverride != nullptr && resourcesPathOverride[0] != '\0')
        {
            checkPaths.emplace_back(resourcesPathOverride);
        }
        checkPaths.push_back(currentPath);
        checkPaths.push_back(currentPath + "/Resources");
        checkPaths.push_back(currentPath + "/../Resources");

        bool didFindResources = false;
        for (auto& checkPath : checkPaths)
        {
            std::cout << ("[AssetManager] Check resources path: " + checkPath) << std::endl;
            if (std::filesystem::exists(checkPath + "/map.bin"))
            {
                std::filesystem::current_path(checkPath);
                std::cout << ("[AssetManager] Resources path found: " + checkPath) << std::endl;
                didFindResources = true;
                break;
            }
        }

        REI_THROW_IF(!didFindResources, std::format("[AssetManager] Resources folder is missing"));
        
        _map = std::make_unique<AssetsMap>(ReadAssetFromBinary<AssetsMap>("map.bin", 0));
    }

    void AssetManager::UnloadAllAssets()
    {
        LOG_DEBUG("UnloadAllAssets requested")
        std::vector<std::string> assetsToDestroy = {};
        {
            const auto records = _registry.GetAllRecords();
            assetsToDestroy.reserve(records.size());
            for (const auto& record : records)
            {
                if (record == nullptr || record->State == AssetState::Unloaded)
                {
                    continue;
                }

                assetsToDestroy.push_back(record->Id);
            }
        }

        {
            _registry.ResetRuntimeTracking();
        }

        LOG_DEBUG("UnloadAllAssets prepared {} assets for destruction", assetsToDestroy.size())
        for (const auto& id : assetsToDestroy)
        {
            LOG_DEBUG("Delete asset id={}", id)
            _registry.MarkForDestruction(id);
        }

        _registry.CollectGarbage();
        _registry.PumpDestroyQueue();
        LOG_DEBUG("UnloadAllAssets completed")
    }

    void AssetManager::DeleteTmpFiles()
    {
        _tmpStorage.DeleteAll();
    }
}
