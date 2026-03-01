#pragma once

#include <memory>
#include <mutex>
#include <typeindex>
#include <unordered_map>
#include <vector>

#include "Common/Primitives.h"
#include "AssetDestroyQueue.h"
#include "Modules/Assets/Core/AssetRef.h"

namespace rei::assets
{
    struct AssetReleaseResult
    {
        bool RefCountReachedZero = false;
        bool MissingLoadedRecord = false;
        i32 ReleasedSize = 0;
    };

    struct AssetUnloadRecord
    {
        std::string Id;
        std::type_index Type = typeid(void);
        i32 Size = 0;
    };

    class AssetRegistry
    {
    public:
        template <typename T>
        void CreateAssetRecord(AssetRef<T>& assetRef, const std::string& name, T* value, i32 assetSize, AssetState state);

        template <typename T>
        std::shared_ptr<AssetRecord> FindRecord(const std::string& id) const;
        REI_API std::shared_ptr<AssetRecord> FindRecord(const std::string& id) const;
        REI_API std::vector<std::shared_ptr<AssetRecord>> GetAllRecords() const;
        REI_API i32 GetRecordCount() const;

        REI_API AssetReleaseResult ReleaseAssetWithId(const std::string& id);
        REI_API std::vector<AssetUnloadRecord> ReleaseAllLoadedAssets();
        
        REI_API void SetRefCount(const std::string& id, i32 count);
        REI_API void IncrementRefCount(const std::string& id);
        REI_API bool DecrementRefCount(const std::string& id);
        
        REI_API i64 GetLoadedAssetsSize() const;
        
        REI_API void ResetRuntimeTracking();

    private:
        REI_API void MarkPendingDestroy(const std::string& id);
        REI_API void TransitionToUnloadedAndReleasePayload(const std::string& id);
        REI_API bool ReleaseRecordOrUnloadInPlace(const std::string& id);
        REI_API void FlushPendingDestroy();
        REI_API void CollectPendingDestroyRecords(std::vector<std::shared_ptr<AssetRecord>>& recordsToDestroy);

        REI_API void AddLoadedAssetsSize(i32 size);
        REI_API void SubtractLoadedAssetsSize(i32 size);
        REI_API std::shared_ptr<AssetRecord> GetOrCreateRecord(const std::string& id, const std::string& name, const std::type_index& type);

        mutable std::mutex _recordsMutex;
        std::unordered_map<std::string, std::shared_ptr<AssetRecord>> _records = {};
        std::unordered_map<std::string, i32> _assetRefCounts = {};
        i64 _loadedAssetsSize = 0;
        AssetDestroyQueue _destroyQueue = {};
    };
}

#include "AssetRegistry.inl"
