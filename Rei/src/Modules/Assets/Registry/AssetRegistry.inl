#pragma once

namespace rei::assets
{
    template <typename T>
    void AssetRegistry::BindOwned(const std::string& id, T* value, const i32 assetSize, const AssetState state)
    {
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        if (id.empty())
        {
            LOG_WARNING_D(std::format("type={}", typeName), "BindOwned skipped: empty id")
            if (value != nullptr)
            {
                delete value;
            }
            return;
        }

        const auto record = GetOrCreateRecord(id, typeid(T));
        if (record == nullptr)
        {
            LOG_ERROR_D(std::format("id={}, type={}", id, typeName), "BindOwned failed: record creation failed")
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
        LOG_DEBUG_D(std::format("id={}, type={}, state={}, size={} B", id, typeName, static_cast<int>(record->State), assetSize), "BindOwned completed")
    }

    template <typename T>
    std::shared_ptr<AssetRecord> AssetRegistry::GetRecord(const std::string& id)
    {
        if (id.empty()) return nullptr;

        return GetOrCreateRecord(id, typeid(T));
    }
}

