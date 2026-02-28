#pragma once
#include "Modules/Resources/AssetBuilder.h"

namespace rei::assets
{
    template <typename T>
    AssetRef<T> AssetManager::GetById(const std::string& id)
    {
        LOG_DEBUG("GetById request id={}, type={}", id, typeid(T).name())
        auto asset = AssetRef<T>(id);
        Load(asset);
        return asset;
    }

    template <typename T>
    AssetRef<T> AssetManager::GetByPath(const std::string& path)
    {
        LOG_DEBUG("GetByPath request path={}, type={}", path, typeid(T).name())
        AssetRef<T> ref(path);
        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            _registry.IncrementRefCount(ref.Id);
            LOG_DEBUG("GetByPath cache hit id={}", ref.Id)
            return ref;
        }

        std::string filePath = path;
        if (filePath.rfind("@", 0) == 0)
        {
            filePath = filePath.substr(1, filePath.size() - 1);
        }

        const auto dest = _tmpStorage.CreateTempPath(filePath);

        std::cout << "\n";
        LOG_DEBUG("Created temp file at {}", dest)

        i64 _ = resources::AssetBuilder().BuildAsset(filePath, dest, 0);

        LoadAndBindFromPath(ref, dest, 0, true);
        RunPostLoad(ref);
        LOG_DEBUG("GetByPath loaded id={} from {}", ref.Id, dest)

        return ref;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAssetWithId(std::string id, Args&&... args)
    {
        LOG_DEBUG("CreateAssetWithId request id={}, type={}", id, typeid(T).name())
        AssetRef<T> asset(id);
        const auto loadedAsset = new T(std::forward<Args>(args)...);
        constexpr i32 runtimeAssetSize = 0;
        _registry.BindOwned<T>(asset.Id, loadedAsset, runtimeAssetSize, AssetState::Loaded);
        _registry.SetRefCount(asset.Id, 1);
        asset.Record = _registry.GetRecord<T>(asset.Id);
        LOG_DEBUG("CreateAssetWithId created id={}, type={}", asset.Id, typeid(T).name())

        return asset;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAsset(Args&&... args)
    {
        std::string id("runtime_asset_" + STRING(_runtimeAssetCounter++));
        LOG_DEBUG("CreateAsset generated id={}, type={}", id, typeid(T).name())
        return CreateAssetWithId<T>(id, std::forward<Args>(args)...);
    }

    template <typename T>
    bool AssetManager::Load(AssetRef<T>& ref)
    {
        LOG_DEBUG("Load request id={}, type={}", ref.Id, typeid(T).name())
        if (!EnsureAssetDataLoaded(ref, true))
        {
            LOG_ERROR("EnsureAssetDataLoaded failed id={}, type={}", ref.Id, typeid(T).name())
            return false;
        }

        const bool didPostLoad = RunPostLoad(ref);
        LOG_DEBUG("Load result id={}, type={}, success={}", ref.Id, typeid(T).name(), didPostLoad)
        return didPostLoad;
    }

    template <typename T>
    void AssetManager::ReleaseById(const std::string& id)
    {
        if (id == "")
        {
            return;
        }

        LOG_DEBUG("ReleaseById request id={}, type={}", id, typeid(T).name())
        if (!_registry.DecrementRefCount(id))
        {
            LOG_DEBUG("ReleaseById deferred id={} (still referenced)", id)
            return;
        }

        const auto record = _registry.FindRecord(id);
        if (record == nullptr || record->State == AssetState::Unloaded)
        {
            LOG_WARNING("ReleaseById missing loaded asset id={}", id)
            return;
        }

        _registry.SubtractLoadedAssetsSize(record->AssetSize);

        LOG_DEBUG("ReleaseById destroying id={}", id)
        _registry.MarkForDestruction(id);
        _registry.CollectGarbage();
        _registry.PumpDestroyQueue();
        LOG_DEBUG("ReleaseById complete id={}", id)
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
            LOG_WARNING("EnsureAssetDataLoaded skipped empty id, type={}", typeid(T).name())
            return false;
        }

        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            LOG_DEBUG("EnsureAssetDataLoaded already loaded id={}, type={}", ref.Id, typeid(T).name())
            return true;
        }

        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            LOG_DEBUG("EnsureAssetDataLoaded cache hit via record id={}, type={}", ref.Id, typeid(T).name())
            return true;
        }

        try
        {
            // if an absolute path to the asset is used instead
            if (ref.Id.rfind("@", 0) == 0)
            {
                ref = GetByPath<T>(ref.Id);
                LOG_DEBUG("EnsureAssetDataLoaded loaded from direct path id={}, type={}", ref.Id, typeid(T).name())
                return true;
            }

            const auto assetInfo = _map->GetAssetInfo(ref.Id);

            LoadAndBindFromPath(ref, assetInfo.Path, assetInfo.Offset, incrementRefCount);
            LOG_DEBUG("EnsureAssetDataLoaded loaded from map id={}, type={}, path={}, offset={}", ref.Id, typeid(T).name(), assetInfo.Path, assetInfo.Offset)
            return true;
        }
        catch (std::exception e)
        {
            LOG_ERROR("Caught exception while trying to load asset id={}\n Exception: {}", ref.Id, e.what())
        }

        return false;
    }

    template <typename T>
    void AssetManager::LoadAndBindFromPath(AssetRef<T>& ref, const std::string& path, const i64 offset, const bool incrementRefCount)
    {
        LOG_DEBUG("LoadAndBindFromPath request id={}, type={}, path={}, offset={}, incrementRefCount={}", ref.Id, typeid(T).name(), path, offset, incrementRefCount)
        ref.Record = _registry.GetRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            LOG_DEBUG("LoadAndBindFromPath early cache hit id={}", ref.Id)
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
                LOG_DEBUG("LoadAndBindFromPath race cache hit id={} (discarded duplicate load)", ref.Id)
                return;
            }
        }

        _registry.BindOwned<T>(ref.Id, loadedAssetPtr, assetSize, AssetState::Loaded);
        _registry.AddLoadedAssetsSize(assetSize);
        _registry.SetRefCount(ref.Id, incrementRefCount ? 1 : 0);
        ref.Record = _registry.GetRecord<T>(ref.Id);

        LOG_DEBUG("Loaded asset id={}, size={} b, total={} Mb", ref.Id, assetSize, _registry.GetLoadedAssetsSize() / 1024 / 1024.0)
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
            LOG_WARNING("RunPostLoad skipped for unloaded ref id={}, type={}", ref.Id, typeid(T).name())
            return false;
        }

        InvokePostLoadIfSupported(*ref.Get());
        LOG_DEBUG("RunPostLoad completed id={}, type={}", ref.Id, typeid(T).name())
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
