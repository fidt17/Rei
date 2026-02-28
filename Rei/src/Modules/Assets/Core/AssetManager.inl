#pragma once
#include <chrono>
#include "Modules/Resources/AssetBuilder.h"

namespace rei::assets
{
    namespace internal
    {
        inline std::string FormatSize(const i64 bytes)
        {
            if (bytes < 1024)
            {
                return std::format("{} B", bytes);
            }

            const double kb = static_cast<double>(bytes) / 1024.0;
            if (kb < 1024.0)
            {
                return std::format("{:.2f} KB", kb);
            }

            const double mb = kb / 1024.0;
            return std::format("{:.2f} MB", mb);
        }

        inline std::string FormatDurationMs(const i64 durationMs)
        {
            if (durationMs < 1000)
            {
                return std::format("{} ms", durationMs);
            }

            const double seconds = static_cast<double>(durationMs) / 1000.0;
            return std::format("{:.2f} sec", seconds);
        }
    }

    template <typename T>
    AssetRef<T> AssetManager::GetById(const std::string& id)
    {
        auto asset = AssetRef<T>(id);
        Load(asset);
        return asset;
    }

    template <typename T>
    AssetRef<T> AssetManager::GetByPath(const std::string& path)
    {
        AssetRef<T> ref(path);
        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            _registry.IncrementRefCount(ref.Id);
            return ref;
        }

        std::string filePath = path;
        if (filePath.rfind("@", 0) == 0)
        {
            filePath = filePath.substr(1, filePath.size() - 1);
        }

        const auto dest = _tmpStorage.CreateTempPath(filePath);

        i64 _ = resources::AssetBuilder().BuildAsset(filePath, dest, 0);

        LoadAndBindFromPath(ref, dest, 0, true);
        RunPostLoad(ref);

        return ref;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAssetWithId(std::string id, Args&&... args)
    {
        AssetRef<T> asset(id);
        const auto loadedAsset = new T(std::forward<Args>(args)...);
        constexpr i32 runtimeAssetSize = 0;
        _registry.BindOwned<T>(asset.Id, loadedAsset, runtimeAssetSize, AssetState::Loaded);
        _registry.SetRefCount(asset.Id, 1);
        asset.Record = _registry.GetRecord<T>(asset.Id);

        return asset;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAsset(Args&&... args)
    {
        std::string id("runtime_asset_" + STRING(_runtimeAssetCounter++));
        return CreateAssetWithId<T>(id, std::forward<Args>(args)...);
    }

    template <typename T>
    bool AssetManager::Load(AssetRef<T>& ref)
    {
        const bool wasLoadedBefore = ref.IsLoaded();
        const auto startedAt = std::chrono::high_resolution_clock::now();
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        if (!EnsureAssetDataLoaded(ref, true))
        {
            LOG_ERROR_D(std::format("id={}, type={}", ref.Id, typeName), "EnsureAssetDataLoaded failed")
            return false;
        }

        const bool loaded = RunPostLoad(ref);
        if (!loaded)
        {
            return false;
        }

        if (!wasLoadedBefore)
        {
            const auto finishedAt = std::chrono::high_resolution_clock::now();
            const auto durationMs = std::chrono::duration_cast<std::chrono::milliseconds>(finishedAt - startedAt).count();
            const i32 size = ref.Record != nullptr ? ref.Record->AssetSize : 0;
            LOG_DEBUG("asset loaded id={} type={} size={} duration={}", ref.Id, typeName, internal::FormatSize(size), internal::FormatDurationMs(durationMs))
        }

        return true;
    }

    template <typename T>
    void AssetManager::ReleaseById(const std::string& id)
    {
        if (id == "")
        {
            return;
        }
        if (_isUnloadingAllAssets.load())
        {
            return;
        }

        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        if (!_registry.DecrementRefCount(id))
        {
            return;
        }

        const auto record = _registry.FindRecord(id);
        if (record == nullptr || record->State == AssetState::Unloaded)
        {
            LOG_WARNING_D(std::format("id={}", id), "ReleaseById missing loaded asset")
            return;
        }

        const i32 size = record->AssetSize;
        _registry.SubtractLoadedAssetsSize(record->AssetSize);
        _registry.MarkForDestruction(id);
        _registry.CollectGarbage();
        _registry.PumpDestroyQueue();
        if (_registry.FindRecord(id) == nullptr)
        {
            LOG_DEBUG("asset unloaded id={} type={} size={}", id, typeName, internal::FormatSize(size))
        }
        else
        {
            _registry.SetUnloaded(id);
            LOG_DEBUG("asset unloaded id={} type={} size={}", id, typeName, internal::FormatSize(size))
        }
    }

    template <typename T>
    void AssetManager::Release(const AssetRef<T>& ref)
    {
        ReleaseById<T>(ref.Id);
    }

    template <typename T>
    bool AssetManager::EnsureAssetDataLoaded(AssetRef<T>& ref, const bool incrementRefCount)
    {
        if (ref.Id == "")
        {
            const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
            LOG_WARNING_D(std::format("type={}", typeName), "EnsureAssetDataLoaded skipped empty id")
            return false;
        }

        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            return true;
        }

        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            return true;
        }

        try
        {
            if (ref.Id.rfind("@", 0) == 0)
            {
                ref = GetByPath<T>(ref.Id);
                return true;
            }

            const auto assetInfo = _map->GetAssetInfo(ref.Id);

            LoadAndBindFromPath(ref, assetInfo.Path, assetInfo.Offset, incrementRefCount);
            return true;
        }
        catch (std::exception e)
        {
            LOG_ERROR_D(std::format("id={}", ref.Id), "EnsureAssetDataLoaded exception: {}", e.what())
        }

        return false;
    }

    template <typename T>
    void AssetManager::LoadAndBindFromPath(AssetRef<T>& ref, const std::string& path, const i64 offset, const bool incrementRefCount)
    {
        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            return;
        }

        auto reader = resources::BinaryReader(path, offset);
        auto* loadedAssetPtr = new T(reader);
        const i64 loadedAssetSize = reader.GetPosition() - offset;
        reader.Close();

        const auto assetSize = static_cast<i32>(loadedAssetSize);
        {
            std::lock_guard lock(_assetsMutex);
            ref.Record = _registry.GetRecord<T>(ref.Id);
            if (ref.IsLoaded())
            {
                delete loadedAssetPtr;
                if (incrementRefCount)
                {
                    _registry.IncrementRefCount(ref.Id);
                }
                return;
            }
        }

        _registry.BindOwned<T>(ref.Id, loadedAssetPtr, assetSize, AssetState::Loaded);
        _registry.AddLoadedAssetsSize(assetSize);
        _registry.SetRefCount(ref.Id, incrementRefCount ? 1 : 0);
        ref.Record = _registry.GetRecord<T>(ref.Id);
    }

    template <typename T>
    bool AssetManager::RunPostLoad(AssetRef<T>& ref)
    {
        if (ref.Record == nullptr)
        {
            ref.Record = _registry.GetRecord<T>(ref.Id);
        }

        if (!ref.IsLoaded())
        {
            const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
            LOG_WARNING_D(std::format("id={}, type={}", ref.Id, typeName), "RunPostLoad skipped for unloaded ref")
            return false;
        }

        InvokePostLoadIfSupported(*ref.Get());
        return true;
    }

    template <typename T>
    void AssetManager::InvokePostLoadIfSupported(T& asset)
    {
        if constexpr (requires { asset.PostLoad(); })
        {
            asset.PostLoad();
        }
    }
}



