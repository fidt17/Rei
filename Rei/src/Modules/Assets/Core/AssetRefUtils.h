#pragma once

#include "AssetManager.h"
#include "Engine/Services.h"

namespace rei::assets
{
    template <typename T>
    void SyncAfterExternalChange(AssetRef<T>& ref)
    {
        const auto boundId = ref.GetBoundId();
        if (boundId == ref.Id) return;

        auto& assetManager = GetAssetManager();

        if (!boundId.empty())
        {
            assetManager.ReleaseById<T>(boundId);
        }

        ref.Record = nullptr;

        if (!ref.Id.empty())
        {
            assetManager.Load(ref);
        }
    }

    template <typename T>
    void ClearAssetRefCollection(std::vector<AssetRef<T>>& collection)
    {
        for (const auto& ref : collection)
        {
            if (ref.Record == nullptr || ref.Record->Value == nullptr) continue;
            GetAssetManager().ReleaseById<T>(ref.Record->Id);
        }
        collection.clear();
    }

    template <typename T>
    void SetAssetRefCollection(std::vector<AssetRef<T>>& collection, const nlohmann::json& data)
    {
        const auto& items = data.get_ref<const nlohmann::json::array_t&>();
        std::vector<AssetRef<T>> replacement;
        replacement.reserve(items.size());
        for (const auto& itemData : items)
        {
            replacement.emplace_back().REI_SET(itemData);
        }

        // Acquire replacements before releasing old refs, including repeated IDs.
        try
        {
            for (auto& ref : replacement)
            {
                SyncAfterExternalChange(ref);
            }
        }
        catch (...)
        {
            ClearAssetRefCollection(replacement);
            throw;
        }

        collection.swap(replacement);
        ClearAssetRefCollection(replacement);
    }

    template <typename T>
    void Assign(AssetRef<T>& target, const AssetRef<T>& other)
    {
        if (&target == &other) return;

        auto& assetManager = GetAssetManager();

        const auto targetBoundId = target.GetBoundId();
        if (!targetBoundId.empty())
        {
            assetManager.ReleaseById<T>(targetBoundId);
        }

        target.Id = other.Id;
        target.Record = other.Record;

        if (!target.Id.empty())
        {
            assetManager.Load(target);
        }
    }

    template <typename T>
    void AutoAssignHandler(AssetRef<T>& target, const AssetRef<T>& other)
    {
        Assign(target, other);
    }

    template <typename T>
    void RegisterAutoAssignHandler()
    {
        AssetRef<T>::AssignHandlerFunc = &AutoAssignHandler<T>;
    }
}

