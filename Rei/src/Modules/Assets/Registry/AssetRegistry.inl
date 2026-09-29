#pragma once

namespace rei::assets
{
    template <typename T>
    void AssetRegistry::CreateAssetRecord(AssetRef<T>& assetRef, const std::string& name, T* value, const i32 assetSize, const AssetState state)
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

        const auto record = GetOrCreateRecord(assetRef.Id, name, typeid(T));
        if (record == nullptr)
        {
            LOG_ERROR("Failed to create asset record, id={}, type={}", assetRef.Id, typeName)
            if (value != nullptr)
            {
                delete value;
            }
            return;
        }

        {
            std::scoped_lock lock(_recordsMutex);
            const auto typedValue = std::shared_ptr<T>(value);
            record->Value = typedValue;
            record->AssetSize = assetSize;
            record->State = value != nullptr ? state : AssetState::Unloaded;
            assetRef.Record = record;

            if constexpr (AssetDataAccessor<T>::IS_SUPPORTED)
            {
                record->DataAccessor = std::make_unique<AssetDataAccessor<T>>(typedValue);
            }
            else
            {
                record->DataAccessor.reset();
            }
        }

        AddLoadedAssetsSize(assetSize);
    }

    template <typename T>
    std::shared_ptr<AssetRecord> AssetRegistry::FindRecord(const std::string& id) const
    {
        const auto record = FindRecord(id);
        if (record == nullptr) return nullptr;

        if (record->Type == typeid(T))
        {
            return record;
        }

        LOG_ERROR(
            "Asset type mismatch id={}, existingType={}, requestedType={}",
            id,
            rei::common::logging::utility::SimplifyTypeName(record->Type.name()),
            rei::common::logging::utility::SimplifyTypeName(typeid(T).name()))
        return nullptr;
    }
}
