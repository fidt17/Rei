#include "pch.h"
#include "AssetRegistry.h"

namespace rei::assets
{
    void AssetRegistry::BindExternal(const std::string& id, const std::type_index& type, void* value, const AssetState state)
    {
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(type.name());
        if (id.empty())
        {
            LOG_WARNING_D(std::format("type={}", typeName), "BindExternal skipped: empty id")
            return;
        }

        const auto record = GetOrCreateRecord(id, type);
        if (record == nullptr)
        {
            LOG_ERROR_D(std::format("id={}, type={}", id, typeName), "BindExternal failed: record creation failed")
            return;
        }

        std::scoped_lock lock(_recordsMutex);
        record->OwnedValue.reset();
        record->ExternalValue = value;
        record->AssetSize = 0;
        record->State = value != nullptr ? state : AssetState::Unloaded;
        record->LastError.clear();
        LOG_DEBUG_D(std::format("id={}, type={}, state={}", id, typeName, static_cast<int>(record->State)), "BindExternal completed")
    }

    void AssetRegistry::SetUnloaded(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);

        const auto existing = _records.find(id);
        if (existing == _records.end())
        {
            LOG_WARNING_D(std::format("id={}", id), "SetUnloaded skipped: missing id")
            return;
        }

        existing->second->OwnedValue.reset();
        existing->second->ExternalValue = nullptr;
        existing->second->AssetSize = 0;
        existing->second->State = AssetState::Unloaded;
        LOG_DEBUG_D(std::format("id={}", id), "SetUnloaded completed")
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
                        rei::common::logging::internal::SimplifyTypeName(existing->second->Type.name()),
                        rei::common::logging::internal::SimplifyTypeName(type.name())),
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
        LOG_DEBUG_D(std::format("id={}, type={}", id, rei::common::logging::internal::SimplifyTypeName(type.name())), "Created asset record")

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
        LOG_DEBUG_D(std::format("id={}", id), "MarkForDestruction")
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

        LOG_DEBUG_D(std::format("count={}", recordsToDestroy.size()), "CollectGarbage ready records")
        for (const auto& record : recordsToDestroy)
        {
            LOG_DEBUG_D(std::format("id={}", record->Id), "CollectGarbage enqueue destroy")
            _destroyQueue.Enqueue(record);
        }
    }

    void AssetRegistry::PumpDestroyQueue()
    {
        LOG_DEBUG_D(std::format("size={}", _destroyQueue.Size()), "PumpDestroyQueue start")
        _destroyQueue.Flush();
        LOG_DEBUG_D(std::format("size={}", _destroyQueue.Size()), "PumpDestroyQueue complete")
    }

    i32 AssetRegistry::GetRecordCount() const
    {
        std::scoped_lock lock(_recordsMutex);
        return static_cast<i32>(_records.size());
    }

    std::shared_ptr<AssetRecord> AssetRegistry::FindRecord(const std::string& id) const
    {
        if (id.empty())
        {
            return nullptr;
        }

        std::scoped_lock lock(_recordsMutex);
        const auto it = _records.find(id);
        if (it == _records.end())
        {
            return nullptr;
        }

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

    void AssetRegistry::AddLoadedAssetsSize(const i32 size)
    {
        std::scoped_lock lock(_recordsMutex);
        _loadedAssetsSize += size;
    }

    void AssetRegistry::SubtractLoadedAssetsSize(const i32 size)
    {
        std::scoped_lock lock(_recordsMutex);
        _loadedAssetsSize -= size;
        if (_loadedAssetsSize < 0)
        {
            _loadedAssetsSize = 0;
        }
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

