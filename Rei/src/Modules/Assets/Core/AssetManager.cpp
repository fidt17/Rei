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

        struct AssetUnloadInfo
        {
            std::string Id;
            std::string TypeName;
            i32 Size = 0;
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
                    .TypeName = common::logging::utility::SimplifyTypeName(record->Type.name()),
                    .Size = record->AssetSize,
                });
            }
        }

        _registry.ResetRuntimeTracking();

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
                LOG_DEBUG("Asset unloaded id={} type={} size={}", asset.Id, asset.TypeName, common::logging::utility::FormatSize(asset.Size))
            }
            else
            {
                _registry.SetUnloaded(asset.Id);
                LOG_DEBUG("Asset unloaded id={} type={} size={}", asset.Id, asset.TypeName, common::logging::utility::FormatSize(asset.Size))
            }
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
