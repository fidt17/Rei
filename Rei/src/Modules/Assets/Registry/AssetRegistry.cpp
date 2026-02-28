#include "pch.h"
#include "AssetRegistry.h"

#include <algorithm>

namespace rei::assets
{
    void AssetRegistry::SetUnloaded(const std::string& id)
    {
        std::shared_ptr<void> valueToRelease = nullptr;
        {
            std::scoped_lock lock(_recordsMutex);

            const auto existing = _records.find(id);
            if (existing == _records.end())
            {
                LOG_WARNING("Cannot set asset id={} state to Unloaded because related asset record is missing", id)
                return;
            }

            valueToRelease = std::move(existing->second->Value);
            existing->second->AssetSize = 0;
            existing->second->State = AssetState::Unloaded;
        }

        // Release owned payload outside the registry lock to avoid re-entrant locking
        // if destructors trigger nested asset manager calls.
        valueToRelease.reset();
    }

    std::shared_ptr<AssetRecord> AssetRegistry::GetOrCreateRecord(const std::string& id, const std::type_index& type)
    {
        std::scoped_lock lock(_recordsMutex);

        const auto existing = _records.find(id);
        if (existing != _records.end())
        {
            if (existing->second->Type != type)
            {
                LOG_ERROR_D(
                    std::format(
                        "id={}, existingType={}, requestedType={}",
                        id,
                        rei::common::logging::utility::SimplifyTypeName(existing->second->Type.name()),
                        rei::common::logging::utility::SimplifyTypeName(type.name())),
                    "Asset type mismatch")
                return nullptr;
            }

            return existing->second;
        }

        auto record = std::make_shared<AssetRecord>();
        record->Id = id;
        record->Type = type;
        record->State = AssetState::Unloaded;
        _records.insert({id, record});

        return record;
    }

    void AssetRegistry::MarkForDestruction(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);

        const auto existing = _records.find(id);
        if (existing == _records.end())
        {
            LOG_WARNING_D(std::format("id={}", id), "MarkForDestruction skipped: missing id")
            return;
        }

        existing->second->State = AssetState::PendingDestroy;
    }

    void AssetRegistry::CollectGarbage()
    {
        std::vector<std::shared_ptr<AssetRecord>> recordsToDestroy;
        {
            std::scoped_lock lock(_recordsMutex);

            auto it = _records.begin();
            while (it != _records.end())
            {
                const auto& record = it->second;
                const bool readyForDestroy = record->State == AssetState::PendingDestroy && record.use_count() == 1;
                if (!readyForDestroy)
                {
                    ++it;
                    continue;
                }

                recordsToDestroy.push_back(record);
                it = _records.erase(it);
            }
        }

        for (const auto& record : recordsToDestroy)
        {
            _destroyQueue.Enqueue(record);
        }
    }

    void AssetRegistry::PumpDestroyQueue()
    {
        _destroyQueue.Flush();
    }

    i32 AssetRegistry::GetRecordCount() const
    {
        std::scoped_lock lock(_recordsMutex);
        return static_cast<i32>(_records.size());
    }

    std::shared_ptr<AssetRecord> AssetRegistry::FindRecord(const std::string& id) const
    {
        if (id.empty()) return nullptr;

        std::scoped_lock lock(_recordsMutex);
        const auto it = _records.find(id);
        if (it == _records.end()) return nullptr;

        return it->second;
    }

    std::vector<std::shared_ptr<AssetRecord>> AssetRegistry::GetAllRecords() const
    {
        std::scoped_lock lock(_recordsMutex);

        std::vector<std::shared_ptr<AssetRecord>> records;
        records.reserve(_records.size());
        for (const auto& [_, record] : _records)
        {
            records.push_back(record);
        }

        return records;
    }

    void AssetRegistry::SetRefCount(const std::string& id, const i32 count)
    {
        std::scoped_lock lock(_recordsMutex);
        _assetRefCounts[id] = count;
    }

    void AssetRegistry::IncrementRefCount(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);
        auto it = _assetRefCounts.find(id);
        if (it == _assetRefCounts.end())
        {
            _assetRefCounts[id] = 1;
            return;
        }

        it->second++;
    }

    bool AssetRegistry::DecrementRefCount(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);
        auto it = _assetRefCounts.find(id);
        if (it == _assetRefCounts.end())
        {
            LOG_WARNING_D(std::format("id={}", id), "Asset refcount decrement requested for missing id")
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

    AssetReleaseResult AssetRegistry::ReleaseAssetWithId(const std::string& id)
    {
        AssetReleaseResult result{};
        result.RefCountReachedZero = DecrementRefCount(id);
        if (!result.RefCountReachedZero) return result;

        const auto record = FindRecord(id);
        if (record == nullptr || record->State == AssetState::Unloaded)
        {
            result.MissingLoadedRecord = true;
            return result;
        }

        result.ReleasedSize = record->AssetSize;
        SubtractLoadedAssetsSize(record->AssetSize);
        
        MarkForDestruction(id);
        CollectGarbage();
        PumpDestroyQueue();
        if (FindRecord(id) != nullptr)
        {
            SetUnloaded(id);
        }

        return result;
    }

    void AssetRegistry::AddLoadedAssetsSize(const i32 size)
    {
        std::scoped_lock lock(_recordsMutex);
        _loadedAssetsSize += size;
    }

    void AssetRegistry::SubtractLoadedAssetsSize(const i32 size)
    {
        std::scoped_lock lock(_recordsMutex);
        _loadedAssetsSize -= size;
        _loadedAssetsSize = std::max<i64>(_loadedAssetsSize, 0);
    }

    i64 AssetRegistry::GetLoadedAssetsSize() const
    {
        std::scoped_lock lock(_recordsMutex);
        return _loadedAssetsSize;
    }

    void AssetRegistry::ResetRuntimeTracking()
    {
        std::scoped_lock lock(_recordsMutex);
        _assetRefCounts.clear();
        _loadedAssetsSize = 0;
    }
}
