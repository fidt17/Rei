#pragma once

#include "AssetManager.h"
#include "Engine/Services.h"

namespace rei::assets
{
    template <typename T>
    void SyncAfterExternalChange(AssetRef<T>& ref)
    {
        if (ref.LoadedId == ref.Id)
        {
            return;
        }

        auto& assetManager = GetAssetManager();

        if (!ref.LoadedId.empty())
        {
            assetManager.ReleaseById<T>(ref.LoadedId);
        }

        ref.Asset = nullptr;
        ref.AssetSize = 0;
        ref.LoadedId.clear();

        if (!ref.Id.empty())
        {
            assetManager.Load(ref);
        }
    }

    template <typename T>
    void Assign(AssetRef<T>& target, const AssetRef<T>& other)
    {
        if (&target == &other)
        {
            return;
        }

        auto& assetManager = GetAssetManager();

        if (!target.LoadedId.empty())
        {
            assetManager.ReleaseById<T>(target.LoadedId);
        }

        target.Id = other.Id;
        target.Asset = other.Asset;
        target.AssetSize = other.AssetSize;
        target.LoadedId = other.LoadedId;

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
