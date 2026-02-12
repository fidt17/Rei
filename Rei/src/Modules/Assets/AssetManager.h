#pragma once

#include <mutex>

#include "AssetRef.h"
#include "AssetsMap.h"
#include "Modules/Resources/Serialization/BinaryReader.h"

namespace rei::assets
{
    SET_LOG_SCOPE("Asset Manager")

    class AssetManager
    {
    public:
        explicit AssetManager();

        template <typename T>
        REI_API AssetRef<T> GetById(const std::string& id);

        template <typename T>
        REI_API AssetRef<T> GetByPath(const std::string& path);

        template <typename T, typename... Args>
        REI_API AssetRef<T> CreateAssetWithId(std::string id, Args... args);

        template <typename T, typename... Args>
        REI_API AssetRef<T> CreateAsset(Args... args);

        template <typename T>
        REI_API bool Load(AssetRef<T>& ref);

        template <typename T>
        REI_API bool LoadData(AssetRef<T>& ref);

        template <typename T>
        REI_API bool PreloadData(AssetRef<T>& ref);

        template <typename T>
        REI_API bool PostLoad(AssetRef<T>& ref);

        template <typename T>
        REI_API bool PreloadPostLoad(AssetRef<T>& ref);

        REI_API void UnloadAllAssets();

        template <typename T>
        REI_API void ReleaseById(const std::string& id);

        REI_API void DeleteTmpFiles() const;

    private:
        std::unique_ptr<AssetsMap> _map;

        u32 _runtimeAssetCounter = 0;
        i64 _loadedAssetsSize = 0;
        mutable std::mutex _assetsMutex;
        std::unordered_map<std::string, IAssetRef*> _loadedAssets{};
        std::unordered_map<std::string, i32> _assetRefCounts{};

        std::vector<std::string> _tmpFiles;

        template <typename T>
        REI_API T Load(const std::string& path, i64 offset);

        template <typename T>
        REI_API T Load(const std::string& path, i64 offset, i64& size);

        template <typename T>
        REI_API bool LoadDataInternal(AssetRef<T>& ref, bool incrementRefCount);

        template <typename T>
        REI_API void LoadDataFromPath(AssetRef<T>& ref, const std::string& path, i64 offset, bool incrementRefCount);

        template <typename T>
        static void PostLoad(T& asset);

        REI_API void IncrementRefCount(const std::string& id);
        REI_API void IncrementRefCountUnsafe(const std::string& id);
        REI_API bool DecrementRefCount(const std::string& id);
        REI_API bool DecrementRefCountUnsafe(const std::string& id);
    };
}

#include "AssetManager.inl"
