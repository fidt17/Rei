#include "pch.h"
#include "AssetRegistry.h"

namespace rei::assets
{
    void AssetRegistry::BindExternal(const std::string& id, const std::type_index& type, void* value, const AssetState state)
    {
        if (id.empty())
        {
            LOG_WARNING("BindExternal skipped: empty id, type={}", type.name())
            return;
        }

        const auto record = GetOrCreateRecord(id, type);
        if (record == nullptr)
        {
            LOG_ERROR("BindExternal failed: record creation failed id={}, type={}", id, type.name())
            return;
        }

        std::scoped_lock lock(_recordsMutex);
        record->OwnedValue.reset();
        record->ExternalValue = value;
        record->AssetSize = 0;
        record->State = value != nullptr ? state : AssetState::Unloaded;
        record->LastError.clear();
        LOG_DEBUG("BindExternal id={}, type={}, state={}, ptr={}", id, type.name(), static_cast<int>(record->State), value)
    }

    void AssetRegistry::SetUnloaded(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);

        const auto existing = _records.find(id);
        if (existing == _records.end())
        {
            LOG_WARNING("SetUnloaded skipped: missing id={}", id)
            return;
        }

        existing->second->OwnedValue.reset();
        existing->second->ExternalValue = nullptr;
        existing->second->AssetSize = 0;
        existing->second->State = AssetState::Unloaded;
        LOG_DEBUG("SetUnloaded id={}", id)
    }

    std::shared_ptr<AssetRecord> AssetRegistry::GetOrCreateRecord(const std::string& id, const std::type_index& type)
    {
        std::scoped_lock lock(_recordsMutex);

        const auto existing = _records.find(id);
        if (existing != _records.end())
        {
            if (existing->second->Type != type)
            {
                LOG_ERROR("Asset type mismatch for id={}. Existing={}, Requested={}", id, existing->second->Type.name(), type.name())
                return nullptr;
            }

            return existing->second;
        }

        auto record = std::make_shared<AssetRecord>();
        record->Id = id;
        record->Type = type;
        record->State = AssetState::Unloaded;
        _records.insert({id, record});
        LOG_DEBUG("Created asset record id={}, type={}", id, type.name())

        return record;
    }

    void AssetRegistry::MarkForDestruction(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);

        const auto existing = _records.find(id);
        if (existing == _records.end())
        {
            LOG_WARNING("MarkForDestruction skipped: missing id={}", id)
            return;
        }

        existing->second->State = AssetState::PendingDestroy;
        LOG_DEBUG("MarkForDestruction id={}", id)
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

        LOG_DEBUG("CollectGarbage records ready for destroy={}", recordsToDestroy.size())
        for (const auto& record : recordsToDestroy)
        {
            LOG_DEBUG("CollectGarbage enqueue destroy id={}", record->Id)
            _destroyQueue.Enqueue(record);
        }
    }

    void AssetRegistry::PumpDestroyQueue()
    {
        LOG_DEBUG("PumpDestroyQueue start size={}", _destroyQueue.Size())
        _destroyQueue.Flush();
        LOG_DEBUG("PumpDestroyQueue complete size={}", _destroyQueue.Size())
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
        LOG_DEBUG("Asset refcount set id={}, count={}", id, count)
    }

    void AssetRegistry::IncrementRefCount(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);
        auto it = _assetRefCounts.find(id);
        if (it == _assetRefCounts.end())
        {
            _assetRefCounts[id] = 1;
            LOG_DEBUG("Asset refcount created id={}, count=1", id)
            return;
        }

        it->second++;
        LOG_DEBUG("Asset refcount increment id={}, count={}", id, it->second)
    }

    bool AssetRegistry::DecrementRefCount(const std::string& id)
    {
        std::scoped_lock lock(_recordsMutex);
        auto it = _assetRefCounts.find(id);
        if (it == _assetRefCounts.end())
        {
            LOG_WARNING("Asset refcount decrement requested for missing id={}", id)
            return true;
        }

        it->second--;
        LOG_DEBUG("Asset refcount decrement id={}, count={}", id, it->second)
        if (it->second <= 0)
        {
            _assetRefCounts.erase(it);
            LOG_DEBUG("Asset refcount reached zero id={}", id)
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

