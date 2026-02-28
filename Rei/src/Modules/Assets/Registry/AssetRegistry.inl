#pragma once

namespace rei::assets
{
    template <typename T>
    void AssetRegistry::CreateAssetRecord(AssetRef<T> assetRef, T* value, const i32 assetSize, const AssetState state)
    {
        const auto typeName = common::logging::utility::SimplifyTypeName(typeid(T).name());
        if (assetRef.Id.empty())
        {
            LOG_ERROR("Failed to create asset record for asset with empty id, type={}", typeName)
            if (value != nullptr)
            {
                delete value;
            }
            return;
        }

        const auto record = GetOrCreateRecord(assetRef.Id, typeid(T));
        if (record == nullptr)
        {
            LOG_ERROR("Failed to create asset record, id={}, type={}", assetRef.Id, typeName)
            if (value != nullptr)
            {
                delete value;
            }
            return;
        }

        std::scoped_lock lock(_recordsMutex);
        record->Value = std::shared_ptr<void>(
            value,
            [](void* ptr)
            {
                delete static_cast<T*>(ptr);
            });
        record->AssetSize = assetSize;
        record->State = value != nullptr ? state : AssetState::Unloaded;
    }

    template <typename T>
    std::shared_ptr<AssetRecord> AssetRegistry::GetRecord(const std::string& id)
    {
        if (id.empty()) return nullptr;

        return GetOrCreateRecord(id, typeid(T));
    }
}
