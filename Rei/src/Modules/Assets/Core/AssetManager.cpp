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
        struct AssetUnloadInfo
        {
            std::string Id;
            std::string TypeName;
            i32 Size = 0;
        };

        auto formatSize = [](const i64 bytes)
        {
            if (bytes < 1024)
            {
                return std::format("{} B", bytes);
            }

            const double kb = static_cast<double>(bytes) / 1024.0;
            if (kb < 1024.0)
            {
                return std::format("{:.2f} KB", kb);
            }

            const double mb = kb / 1024.0;
            return std::format("{:.2f} MB", mb);
        };

        std::vector<AssetUnloadInfo> assetsToDestroy = {};
        {
            const auto records = _registry.GetAllRecords();
            assetsToDestroy.reserve(records.size());
            for (const auto& record : records)
            {
                if (record == nullptr || record->State == AssetState::Unloaded)
                {
                    continue;
                }

                assetsToDestroy.push_back({
                    .Id = record->Id,
                    .TypeName = rei::common::logging::internal::SimplifyTypeName(record->Type.name()),
                    .Size = record->AssetSize,
                });
            }
        }

        {
            _registry.ResetRuntimeTracking();
        }

        for (const auto& asset : assetsToDestroy)
        {
            _registry.MarkForDestruction(asset.Id);
        }

        _registry.CollectGarbage();
        _registry.PumpDestroyQueue();

        for (const auto& asset : assetsToDestroy)
        {
            if (_registry.FindRecord(asset.Id) == nullptr)
            {
                LOG_DEBUG("asset unloaded id={} type={} size={}", asset.Id, asset.TypeName, formatSize(asset.Size))
            }
            else
            {
                LOG_WARNING_D(
                    std::format("id={}, type={}", asset.Id, asset.TypeName),
                    "Shutdown unload skipped (still referenced)")
            }
        }
    }

    void AssetManager::DeleteTmpFiles()
    {
        _tmpStorage.DeleteAll();
    }
}
