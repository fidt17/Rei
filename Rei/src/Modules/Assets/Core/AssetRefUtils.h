#pragma once

#include "AssetManager.h"
#include "Engine/Services.h"

namespace rei::assets
{
    template <typename T>
    void SyncAfterExternalChange(AssetRef<T>& ref)
    {
        if (ref.IsLoaded()) return;
        ref.Reset();
        if (!ref.Id.empty()) GetAssetManager().Load(ref);
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

        // Resolve replacements before old refs leave scope, including repeated IDs.
        for (auto& ref : replacement) SyncAfterExternalChange(ref);
        collection.swap(replacement);
    }

    template <typename T>
    void Assign(AssetRef<T>& target, const AssetRef<T>& other)
    {
        if (&target == &other) return;

        auto& assetManager = GetAssetManager();

        // Acquire first: source and target can refer to the same loaded payload.
        AssetRef<T> replacement(other);
        if (!replacement.Id.empty()) assetManager.Load(replacement);
        target.Swap(replacement);
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

