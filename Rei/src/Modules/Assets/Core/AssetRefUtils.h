#pragma once

#include "AssetManager.h"
#include "Engine/Services.h"

namespace rei::assets
{
    template <typename T>
    void SyncAfterExternalChange(AssetRef<T>& ref)
    {
        const auto boundId = ref.GetBoundId();
        if (boundId == ref.Id)
        {
            return;
        }

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
    void Assign(AssetRef<T>& target, const AssetRef<T>& other)
    {
        if (&target == &other)
        {
            return;
        }

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

