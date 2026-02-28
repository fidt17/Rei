#include "pch.h"
#include "AssetManager.h"

namespace rei::assets
{
    AssetManager::AssetManager()
    {
        _map.Initialize();
    }

    void AssetManager::UnloadAllAssets()
    {
        struct UnloadGuard
        {
            explicit UnloadGuard(std::atomic<bool>& flagRef)
                : flag(flagRef)
            {
                flag.store(true);
            }

            ~UnloadGuard()
            {
                flag.store(false);
            }

            std::atomic<bool>& flag;
        };

        UnloadGuard unloadGuard(_isUnloadingAllAssets);

        const auto assetsToUnload = _registry.ReleaseAllLoadedAssets();
        for (const auto& asset : assetsToUnload)
        {
            const auto typeName = common::logging::utility::SimplifyTypeName(asset.Type.name());
            LOG_DEBUG("Asset unloaded id={} type={} size={}", asset.Id, typeName, common::logging::utility::FormatSize(asset.Size))
        }
    }

    void AssetManager::DeleteTmpFiles()
    {
        _tmpStorage.DeleteAll();
    }

    i64 AssetManager::GetLoadedAssetsSize() const
    {
        return _registry.GetLoadedAssetsSize();
    }

    i32 AssetManager::GetLoadedAssetCount() const
    {
        const auto records = _registry.GetAllRecords();
        i32 loadedCount = 0;
        for (const auto& record : records)
        {
            if (record == nullptr || record->State != AssetState::Loaded) continue;

            loadedCount++;
        }

        return loadedCount;
    }
}
