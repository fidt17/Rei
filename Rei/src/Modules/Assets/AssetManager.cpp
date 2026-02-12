#include "pch.h"
#include "AssetManager.h"

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
        
        _map = std::make_unique<AssetsMap>(Load<AssetsMap>("map.bin", 0));
    }

    void AssetManager::UnloadAllAssets()
    {
        std::lock_guard lock(_assetsMutex);
        for (auto loadedAsset : _loadedAssets)
        {
            LOG("Delete asset id={}", loadedAsset.first)
            _loadedAssetsSize -= loadedAsset.second->GetAssetSize();

            loadedAsset.second->UnloadAsset();
            delete loadedAsset.second;
        }

        _assetRefCounts.clear();           
    }

    void AssetManager::DeleteTmpFiles() const
    {
        for (const auto& tmpFile : _tmpFiles)
        {
            LOG_WARNING("Deleted temp file at {}", tmpFile)
            remove(tmpFile.c_str());
        }
    }

    void AssetManager::IncrementRefCount(const std::string& id)
    {
        std::lock_guard lock(_assetsMutex);
        IncrementRefCountUnsafe(id);
    }

    void AssetManager::IncrementRefCountUnsafe(const std::string& id)
    {
        auto it = _assetRefCounts.find(id);
        if (it == _assetRefCounts.end())
        {
            _assetRefCounts[id] = 1;
            return;
        }

        it->second++;
    }

    bool AssetManager::DecrementRefCount(const std::string& id)
    {
        std::lock_guard lock(_assetsMutex);
        return DecrementRefCountUnsafe(id);
    }

    bool AssetManager::DecrementRefCountUnsafe(const std::string& id)
    {
        auto it = _assetRefCounts.find(id);
        if (it == _assetRefCounts.end())
        {
            return true;
        }

        it->second--;
        if (it->second <= 0)
        {
            _assetRefCounts.erase(it);
            return true;
        }

        return false;
    }
}
