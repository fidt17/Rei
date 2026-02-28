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

        LOG_DEBUG("SyncAfterExternalChange type={}, boundId={}, targetId={}", typeid(T).name(), boundId, ref.Id)
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
        LOG_DEBUG("SyncAfterExternalChange complete type={}, id={}", typeid(T).name(), ref.Id)
    }

    template <typename T>
    void Assign(AssetRef<T>& target, const AssetRef<T>& other)
    {
        if (&target == &other)
        {
            return;
        }

        LOG_DEBUG("AssetRef Assign type={}, targetId={}, otherId={}", typeid(T).name(), target.Id, other.Id)
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
        LOG_DEBUG("AssetRef Assign complete type={}, targetId={}", typeid(T).name(), target.Id)
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

