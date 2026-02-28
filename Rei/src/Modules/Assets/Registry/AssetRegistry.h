#pragma once

#include <memory>
#include <mutex>
#include <typeindex>
#include <unordered_map>
#include <vector>

#include "Common/Primitives.h"
#include "AssetDestroyQueue.h"

namespace rei::assets
{
    class AssetRegistry
    {
    public:
        template <typename T>
        void BindOwned(const std::string& id, T* value, i32 assetSize, AssetState state);

        template <typename T>
        std::shared_ptr<AssetRecord> GetRecord(const std::string& id);

        REI_API void BindExternal(const std::string& id, const std::type_index& type, void* value, AssetState state);
        REI_API void SetUnloaded(const std::string& id);
        REI_API void MarkForDestruction(const std::string& id);
        REI_API void CollectGarbage();
        REI_API void PumpDestroyQueue();
        REI_API i32 GetRecordCount() const;
        REI_API std::shared_ptr<AssetRecord> FindRecord(const std::string& id) const;
        REI_API std::vector<std::shared_ptr<AssetRecord>> GetAllRecords() const;
        REI_API void SetRefCount(const std::string& id, i32 count);
        REI_API void IncrementRefCount(const std::string& id);
        REI_API bool DecrementRefCount(const std::string& id);
        REI_API void AddLoadedAssetsSize(i32 size);
        REI_API void SubtractLoadedAssetsSize(i32 size);
        REI_API i64 GetLoadedAssetsSize() const;
        REI_API void ResetRuntimeTracking();

    private:
        REI_API std::shared_ptr<AssetRecord> GetOrCreateRecord(const std::string& id, const std::type_index& type);

        mutable std::mutex _recordsMutex;
        std::unordered_map<std::string, std::shared_ptr<AssetRecord>> _records = {};
        std::unordered_map<std::string, i32> _assetRefCounts = {};
        i64 _loadedAssetsSize = 0;
        AssetDestroyQueue _destroyQueue = {};
    };
}

#include "AssetRegistry.inl"
