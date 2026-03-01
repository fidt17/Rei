#pragma once

#include <memory>
#include <atomic>
#include <mutex>
#include <string>
#include <utility>
#include <vector>

#include "Modules/Assets/Registry/AssetRegistry.h"
#include "AssetPostLoadHandler.h"
#include "AssetRef.h"
#include "AssetTmpStorage.h"
#include "Modules/Assets/Storage/AssetsMap.h"
#include "Modules/Resources/Serialization/BinaryReader.h"

namespace rei::scenes
{
    class SceneAssetPreloader;
}

namespace rei::assets
{

    class AssetManager
    {
    public:

        explicit AssetManager();

        template <typename T>
        REI_API AssetRef<T> GetById(const std::string& id);

        template <typename T>
        REI_API AssetRef<T> GetByPath(const std::string& path);

        template <typename T, typename... Args>
        REI_API AssetRef<T> CreateAssetWithId(std::string id, Args&&... args);

        template <typename T, typename... Args>
        REI_API AssetRef<T> CreateAsset(Args&&... args);

        template <typename T>
        REI_API bool Load(AssetRef<T>& ref);

        REI_API void UnloadAllAssets();

        template <typename T>
        REI_API void ReleaseById(const std::string& id);
        
        template <typename T>
        REI_API void Release(const AssetRef<T>& ref);

        REI_API i64 GetLoadedAssetsSize() const;
        REI_API i32 GetLoadedAssetCount() const;

        REI_API void DeleteTmpFiles();

    private:
        friend class scenes::SceneAssetPreloader;

        AssetsMap _map;
        AssetRegistry _registry = {};
        AssetTmpStorage _tmpStorage = {};
        AssetPostLoadHandler _postLoadHandler = {};

        u32 _runtimeAssetCounter = 0;
        mutable std::mutex _assetsMutex;
        std::atomic<bool> _isUnloadingAllAssets = false;

        template <typename T>
        REI_API void QueueDeferredPostLoad(const std::string& id);

        REI_API bool FlushDeferredPostLoads();

        template <typename T>
        REI_API bool LoadInternal(AssetRef<T>& ref, bool incrementRefCount);

        template <typename T>
        REI_API void LoadAndCreateRecord(AssetRef<T>& ref, const std::string& name, const std::string& path, i64 offset, bool incrementRefCount);

        template <typename T>
        REI_API bool RunPostLoad(AssetRef<T>& ref);

        template <typename T>
        static void InvokePostLoadIfSupported(T& asset);
    };
}

#include "AssetManager.inl"


