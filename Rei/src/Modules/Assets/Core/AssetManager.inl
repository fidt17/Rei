#pragma once
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
    }

    template <typename T>
    AssetRef<T> AssetManager::GetById(const std::string& id)
    {
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("id={}, type={}", id, typeName), "GetById request")
        auto asset = AssetRef<T>(id);
        Load(asset);
        return asset;
    }

    template <typename T>
    AssetRef<T> AssetManager::GetByPath(const std::string& path)
    {
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("path={}, type={}", path, typeName), "GetByPath request")
        AssetRef<T> ref(path);
        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            _registry.IncrementRefCount(ref.Id);
            LOG_DEBUG_D(std::format("id={}", ref.Id), "GetByPath cache hit")
            return ref;
        }

        std::string filePath = path;
        if (filePath.rfind("@", 0) == 0)
        {
            filePath = filePath.substr(1, filePath.size() - 1);
        }

        const auto dest = _tmpStorage.CreateTempPath(filePath);

        std::cout << "\n";
        LOG_DEBUG_D(std::format("path={}", dest), "Created temp file")

        i64 _ = resources::AssetBuilder().BuildAsset(filePath, dest, 0);

        LoadAndBindFromPath(ref, dest, 0, true);
        RunPostLoad(ref);
        LOG_DEBUG_D(std::format("id={}, path={}", ref.Id, dest), "GetByPath loaded")

        return ref;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAssetWithId(std::string id, Args&&... args)
    {
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("id={}, type={}", id, typeName), "CreateAssetWithId request")
        AssetRef<T> asset(id);
        const auto loadedAsset = new T(std::forward<Args>(args)...);
        constexpr i32 runtimeAssetSize = 0;
        _registry.BindOwned<T>(asset.Id, loadedAsset, runtimeAssetSize, AssetState::Loaded);
        _registry.SetRefCount(asset.Id, 1);
        asset.Record = _registry.GetRecord<T>(asset.Id);
        LOG_DEBUG_D(std::format("id={}, type={}", asset.Id, typeName), "CreateAssetWithId created")

        return asset;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAsset(Args&&... args)
    {
        std::string id("runtime_asset_" + STRING(_runtimeAssetCounter++));
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("id={}, type={}", id, typeName), "CreateAsset generated id")
        return CreateAssetWithId<T>(id, std::forward<Args>(args)...);
    }

    template <typename T>
    bool AssetManager::Load(AssetRef<T>& ref)
    {
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("id={}, type={}", ref.Id, typeName), "Load request")
        if (!EnsureAssetDataLoaded(ref, true))
        {
            LOG_ERROR_D(std::format("id={}, type={}", ref.Id, typeName), "EnsureAssetDataLoaded failed")
            return false;
        }

        const bool didPostLoad = RunPostLoad(ref);
        LOG_DEBUG_D(std::format("id={}, type={}, success={}", ref.Id, typeName, didPostLoad), "Load result")
        return didPostLoad;
    }

    template <typename T>
    void AssetManager::ReleaseById(const std::string& id)
    {
        if (id == "")
        {
            return;
        }

        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("id={}, type={}", id, typeName), "ReleaseById request")
        if (!_registry.DecrementRefCount(id))
        {
            LOG_DEBUG_D(std::format("id={}", id), "ReleaseById deferred (still referenced)")
            return;
        }

        const auto record = _registry.FindRecord(id);
        if (record == nullptr || record->State == AssetState::Unloaded)
        {
            LOG_WARNING_D(std::format("id={}", id), "ReleaseById missing loaded asset")
            return;
        }

        _registry.SubtractLoadedAssetsSize(record->AssetSize);

        LOG_DEBUG_D(std::format("id={}", id), "ReleaseById destroying")
        _registry.MarkForDestruction(id);
        _registry.CollectGarbage();
        _registry.PumpDestroyQueue();
        LOG_DEBUG_D(std::format("id={}", id), "ReleaseById complete")
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
                const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
                LOG_DEBUG_D(std::format("id={}, type={}", ref.Id, typeName), "EnsureAssetDataLoaded loaded from direct path")
                return true;
            }

            const auto assetInfo = _map->GetAssetInfo(ref.Id);

            LoadAndBindFromPath(ref, assetInfo.Path, assetInfo.Offset, incrementRefCount);
            const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
            LOG_DEBUG_D(std::format("id={}, type={}, path={}, offset={}", ref.Id, typeName, assetInfo.Path, assetInfo.Offset), "EnsureAssetDataLoaded loaded from map")
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
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("id={}, type={}, path={}, offset={}, incrementRefCount={}", ref.Id, typeName, path, offset, incrementRefCount), "LoadAndBindFromPath request")
        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            LOG_DEBUG_D(std::format("id={}", ref.Id), "LoadAndBindFromPath cache hit")
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
                LOG_DEBUG_D(std::format("id={}", ref.Id), "LoadAndBindFromPath race cache hit (discarded duplicate load)")
                return;
            }
        }

        _registry.BindOwned<T>(ref.Id, loadedAssetPtr, assetSize, AssetState::Loaded);
        _registry.AddLoadedAssetsSize(assetSize);
        _registry.SetRefCount(ref.Id, incrementRefCount ? 1 : 0);
        ref.Record = _registry.GetRecord<T>(ref.Id);

        const auto loadedTotal = _registry.GetLoadedAssetsSize();
        LOG_DEBUG_D(
            std::format("id={}, size={}, total={}", ref.Id, internal::FormatSize(assetSize), internal::FormatSize(loadedTotal)),
            "Loaded asset")
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
        const auto typeName = rei::common::logging::internal::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG_D(std::format("id={}, type={}", ref.Id, typeName), "RunPostLoad completed")
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



