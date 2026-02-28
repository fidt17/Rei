#pragma once

namespace rei::assets
{
    template <typename T>
    void AssetRegistry::BindOwned(const std::string& id, T* value, const i32 assetSize, const AssetState state)
    {
        if (id.empty())
        {
            LOG_WARNING("BindOwned skipped: empty id, type={}", typeid(T).name())
            if (value != nullptr)
            {
                delete value;
            }
            return;
        }

        const auto record = GetOrCreateRecord(id, typeid(T));
        if (record == nullptr)
        {
            LOG_ERROR("BindOwned failed: record creation failed id={}, type={}", id, typeid(T).name())
            if (value != nullptr)
            {
                delete value;
            }
            return;
        }

        std::scoped_lock lock(_recordsMutex);
        record->OwnedValue = std::shared_ptr<void>(
            value,
            [](void* ptr)
            {
                delete static_cast<T*>(ptr);
            });
        record->ExternalValue = nullptr;
        record->AssetSize = assetSize;
        record->State = value != nullptr ? state : AssetState::Unloaded;
        record->LastError.clear();
        LOG_DEBUG("BindOwned id={}, type={}, state={}, size={} b", id, typeid(T).name(), static_cast<int>(record->State), assetSize)
    }

    template <typename T>
    std::shared_ptr<AssetRecord> AssetRegistry::GetRecord(const std::string& id)
    {
        if (id.empty()) return nullptr;

        return GetOrCreateRecord(id, typeid(T));
    }
}

