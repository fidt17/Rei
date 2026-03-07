#pragma once
#include <chrono>
#include "Common/Time/Stopwatch.h"
#include "Modules/Resources/AssetBuilder.h"

namespace rei::assets
{
    template <typename T>
    AssetRef<T> AssetManager::GetById(const std::string& id)
    {
        auto asset = AssetRef<T>(id);
        asset.Record = _registry.FindRecord<T>(id);
        
        Load(asset);

        return asset;
    }

    template <typename T>
    AssetRef<T> AssetManager::GetByPath(const std::string& path)
    {
        AssetRef<T> ref(path);
        ref.Record = _registry.FindRecord<T>(ref.Id);
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

        LoadAndCreateRecord(ref, path, dest, 0, true);
        if (AssetPostLoadHandler::IsSuppressedForCurrentThread())
        {
            QueueDeferredPostLoad<T>(ref.Id);
        }
        else
        {
            RunPostLoad(ref);
        }

        return ref;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAssetWithId(std::string id, Args&&... args)
    {
        LOG_DEBUG("Creating asset id={}", id)
        
        AssetRef<T> asset(id);
        const auto loadedAsset = new T(std::forward<Args>(args)...);
        constexpr i32 runtimeAssetSize = 0;
        _registry.CreateAssetRecord<T>(asset, id, loadedAsset, runtimeAssetSize, AssetState::Loaded);
        _registry.SetRefCount(asset.Id, 1);
        
        if (AssetPostLoadHandler::IsSuppressedForCurrentThread())
        {
            QueueDeferredPostLoad<T>(asset.Id);
        }
        else
        {
            RunPostLoad(asset);
        }

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
        time::Stopwatch stopwatch;
        stopwatch.Start();
        const auto typeName = common::logging::utility::SimplifyTypeName(typeid(T).name());
        if (!LoadInternal(ref, true))
        {
            LOG_ERROR("Failed to load asset name={}, id={}, type={}", ref.GetName(), ref.Id, typeName)
            return false;
        }

        if (AssetPostLoadHandler::IsSuppressedForCurrentThread())
        {
            QueueDeferredPostLoad<T>(ref.Id);
            return true;
        }

        const bool loaded = RunPostLoad(ref);
        if (!loaded)
        {
            LOG_ERROR("Failed to load asset name={}, id={}, type={}", ref.GetName(), ref.Id, typeName)
            return false;
        }

        if (!wasLoadedBefore)
        {
            stopwatch.Stop();
            const auto durationMs = static_cast<i64>(stopwatch.ElapsedMs());
            const i32 size = ref.Record != nullptr ? ref.Record->AssetSize : 0;
            LOG_DEBUG("Asset loaded name={}, id={} type={} size={} duration={}", ref.GetName(), ref.Id, typeName, common::logging::utility::FormatSize(size),
                      common::logging::utility::FormatDurationMs(durationMs))
        }

        return true;
    }

    template <typename T>
    void AssetManager::ReleaseById(const std::string& id)
    {
        if (id == "") return;
        if (_isUnloadingAllAssets.load()) return;

        const auto releaseResult = _registry.ReleaseAssetWithId(id);
        if (!releaseResult.RefCountReachedZero) return;
        if (releaseResult.MissingLoadedRecord)
        {
            LOG_WARNING("Cannot release asset id={} because it's record is missing", id)
            return;
        }

        const auto typeName = common::logging::utility::SimplifyTypeName(typeid(T).name());
        LOG_DEBUG("Asset unloaded id={} type={} size={}", id, typeName, common::logging::utility::FormatSize(releaseResult.ReleasedSize))
    }

    template <typename T>
    void AssetManager::Release(const AssetRef<T>& ref)
    {
        ReleaseById<T>(ref.Id);
    }

    template <typename T>
    bool AssetManager::LoadInternal(AssetRef<T>& ref, const bool incrementRefCount)
    {
        if (ref.Id == "")
        {
            const auto typeName = common::logging::utility::SimplifyTypeName(typeid(T).name());
            LOG_ERROR("Cannot load asset since it has empty Id, name={}, type={}", ref.GetName(), typeName)
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

        ref.Record = _registry.FindRecord<T>(ref.Id);
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

            const auto assetInfo = _map.GetAssetInfo(ref.Id);

            LoadAndCreateRecord(ref, assetInfo.AssetName, assetInfo.Path, assetInfo.Offset, incrementRefCount);
            return true;
        }
        catch (std::exception e)
        {
            LOG_ERROR("Failed to load asset name={}, id={}\n exception: {}", ref.GetName(), ref.Id, e.what())
        }

        return false;
    }

    template <typename T>
    void AssetManager::LoadAndCreateRecord(AssetRef<T>& ref, const std::string& name, const std::string& path, const i64 offset, const bool incrementRefCount)
    {
        ref.Record = _registry.FindRecord<T>(ref.Id);
        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                _registry.IncrementRefCount(ref.Id);
            }
            return;
        }

        LOG_DEBUG("Loading asset name={}, id={}", name, ref.Id)
        
        auto reader = resources::BinaryReader(path, offset);
        auto* loadedAssetPtr = new T(reader);
        const i64 loadedAssetSize = reader.GetPosition() - offset;
        reader.Close();

        const auto assetSize = static_cast<i32>(loadedAssetSize);
        {
            std::lock_guard lock(_assetsMutex);
            ref.Record = _registry.FindRecord<T>(ref.Id);
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

        _registry.CreateAssetRecord<T>(ref, name, loadedAssetPtr, assetSize, AssetState::Loaded);
        _registry.SetRefCount(ref.Id, incrementRefCount ? 1 : 0);
    }

    template <typename T>
    bool AssetManager::RunPostLoad(AssetRef<T>& ref)
    {
        if (ref.Record == nullptr)
        {
            ref.Record = _registry.FindRecord<T>(ref.Id);
        }

        if (!ref.IsLoaded())
        {
            const auto typeName = common::logging::utility::SimplifyTypeName(typeid(T).name());
            LOG_WARNING("Cannot run post load for unloaded asset name={}, id={}, type={}", ref.GetName(), ref.Id, typeName)
            return false;
        }

        try
        {
            InvokePostLoadIfSupported(*ref.Get());
            return true;
        }
        catch (std::exception e)
        {
            ref.Record->State = AssetState::Failed;
            LOG_ERROR("Failed to run post load for asset name={}, id={}"
                      "\nException: {}", ref.GetName(), ref.Id, e.what())
            return false;
        }
    }

    template <typename T>
    void AssetManager::InvokePostLoadIfSupported(T& asset)
    {
        if constexpr (requires { asset.PostLoad(); })
        {
            asset.PostLoad();
        }
    }

    template <typename T>
    void AssetManager::QueueDeferredPostLoad(const std::string& id)
    {
        LOG_DEBUG("Deferring post-load for asset id={}", id)
        
        _postLoadHandler.Queue(id, [this, id]()
        {
            auto ref = AssetRef<T>(id);
            return RunPostLoad(ref);
        });
    }
}
