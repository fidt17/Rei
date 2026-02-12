#pragma once
#include "Modules/Resources/AssetBuilder.h"

namespace rei::assets
{
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
        {
            std::lock_guard lock(_assetsMutex);

            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset != _loadedAssets.end())
            {
                ref.Asset = static_cast<AssetRef<T>*>(loadedAsset->second)->Asset;
                ref.AssetSize = static_cast<AssetRef<T>*>(loadedAsset->second)->AssetSize;
                ref.LoadedId = ref.Id;
                IncrementRefCountUnsafe(ref.Id);
                return ref;
            }
        }

        std::string filePath = path;
        if (filePath.rfind("@", 0) == 0)
        {
            filePath = filePath.substr(1, filePath.size() - 1);
        }

        const auto baseFilename = filePath.substr(filePath.find_last_of("/\\") + 1);
        const auto dirPath = std::filesystem::temp_directory_path().string() + "Rei Engine\\";
        const auto dest = dirPath + baseFilename + "_" + std::to_string(_tmpFiles.size()) + ".data";
        _tmpFiles.push_back(dest);

        std::filesystem::create_directory(dirPath);
        remove(dest.c_str());

        std::cout << "\n";
        LOG("Created temp file at {}", dest)

        i64 _ = resources::AssetBuilder().BuildAsset(filePath, dest, 0);

        LoadDataFromPath(ref, dest, 0, true);
        PostLoad(ref);

        return ref;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAssetWithId(std::string id, Args... args)
    {
        AssetRef<T>* asset = new AssetRef<T>(id);

        asset->Asset = new T(args...);
        asset->LoadedId = asset->Id;
        {
            std::lock_guard lock(_assetsMutex);
            _loadedAssets[asset->Id] = asset;
            _assetRefCounts[asset->Id] = 1;
        }

        return *asset;
    }

    template <typename T, typename... Args>
    AssetRef<T> AssetManager::CreateAsset(Args... args)
    {
        std::string id("runtime_asset_" + STRING(_runtimeAssetCounter++));
        return CreateAssetWithId<T>(id, args...);
    }

    template <typename T>
    bool AssetManager::Load(AssetRef<T>& ref)
    {
        if (!LoadData(ref))
        {
            return false;
        }

        return PostLoad(ref);
    }

    template <typename T>
    bool AssetManager::LoadData(AssetRef<T>& ref)
    {
        return LoadDataInternal(ref, true);
    }

    template <typename T>
    bool AssetManager::PreloadData(AssetRef<T>& ref)
    {
        return LoadDataInternal(ref, false);
    }

    template <typename T>
    bool AssetManager::PostLoad(AssetRef<T>& ref)
    {
        if (!ref.Asset)
        {
            std::lock_guard lock(_assetsMutex);
            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset == _loadedAssets.end())
            {
                return false;
            }

            ref.Asset = static_cast<AssetRef<T>*>(loadedAsset->second)->Asset;
            ref.AssetSize = static_cast<AssetRef<T>*>(loadedAsset->second)->AssetSize;
            ref.LoadedId = ref.Id;
        }

        PostLoad(*ref.Asset);
        return true;
    }

    template <typename T>
    bool AssetManager::PreloadPostLoad(AssetRef<T>& ref)
    {
        return PostLoad(ref);
    }

    template <typename T>
    void AssetManager::ReleaseById(const std::string& id)
    {
        if (id == "")
        {
            return;
        }

        std::lock_guard lock(_assetsMutex);

        if (!DecrementRefCountUnsafe(id))
        {
            return;
        }

        auto loadedAsset = _loadedAssets.find(id);
        if (loadedAsset == _loadedAssets.end())
        {
            return;
        }

        _loadedAssetsSize -= loadedAsset->second->GetAssetSize();
        loadedAsset->second->UnloadAsset();
        delete loadedAsset->second;
        _loadedAssets.erase(id);
    }

    template <typename T>
    T AssetManager::Load(const std::string& path, const i64 offset)
    {
        i64 size;
        return Load<T>(path, offset, size);
    }

    template <typename T>
    T AssetManager::Load(const std::string& path, const i64 offset, i64& size)
    {
        auto reader = resources::BinaryReader(path, offset);
        auto asset = reader.Get<T>();

        size = reader.GetPosition() - offset;

        reader.Close();

        return asset;
    }

    template <typename T>
    bool AssetManager::LoadDataInternal(AssetRef<T>& ref, const bool incrementRefCount)
    {
        if (ref.Id == "")
        {
            return false;
        }

        if (ref.IsLoaded())
        {
            if (incrementRefCount)
            {
                IncrementRefCount(ref.Id);
            }
            return true;
        }

        {
            std::lock_guard lock(_assetsMutex);

            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset != _loadedAssets.end())
            {
                ref.Asset = static_cast<AssetRef<T>*>(loadedAsset->second)->Asset;
                ref.AssetSize = static_cast<AssetRef<T>*>(loadedAsset->second)->AssetSize;
                ref.LoadedId = ref.Id;
                if (incrementRefCount)
                {
                    IncrementRefCountUnsafe(ref.Id);
                }
                return true;
            }
        }

        try
        {
            // if an absolute path to the asset is used instead
            if (ref.Id.rfind("@", 0) == 0)
            {
                ref = GetByPath<T>(ref.Id);
                return true;
            }

            const auto assetInfo = _map->GetAssetInfo(ref.Id);

            LoadDataFromPath(ref, assetInfo.Path, assetInfo.Offset, incrementRefCount);
            return true;
        }
        catch (std::exception e)
        {
            LOG_ERROR("Caught exception while trying to load asset id={}\n Exception: {}", ref.Id, e.what())
        }

        return false;
    }

    template <typename T>
    void AssetManager::LoadDataFromPath(AssetRef<T>& ref, const std::string& path, const i64 offset, const bool incrementRefCount)
    {
        {
            std::lock_guard lock(_assetsMutex);
            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset != _loadedAssets.end())
            {
                ref.Asset = static_cast<AssetRef<T>*>(loadedAsset->second)->Asset;
                ref.AssetSize = static_cast<AssetRef<T>*>(loadedAsset->second)->AssetSize;
                ref.LoadedId = ref.Id;
                if (incrementRefCount)
                {
                    IncrementRefCountUnsafe(ref.Id);
                }
                return;
            }
        }

        auto reader = resources::BinaryReader(path, offset);
        auto* loadedAssetPtr = new T(reader);
        const i64 loadedAssetSize = reader.GetPosition() - offset;
        ref.LoadedId = ref.Id;
        reader.Close();

        std::lock_guard lock(_assetsMutex);
        auto existingAsset = _loadedAssets.find(ref.Id);
        if (existingAsset != _loadedAssets.end())
        {
            delete loadedAssetPtr;

            ref.Asset = static_cast<AssetRef<T>*>(existingAsset->second)->Asset;
            ref.AssetSize = static_cast<AssetRef<T>*>(existingAsset->second)->AssetSize;
            ref.LoadedId = ref.Id;
            if (incrementRefCount)
            {
                IncrementRefCountUnsafe(ref.Id);
            }
            return;
        }

        ref.Asset = loadedAssetPtr;
        ref.AssetSize = static_cast<i32>(loadedAssetSize);

        auto storedRef = new AssetRef<T>(ref);
        storedRef->Asset = ref.Asset;
        storedRef->AssetSize = ref.AssetSize;
        storedRef->LoadedId = ref.LoadedId;
        _loadedAssets[ref.Id] = storedRef;

        _loadedAssetsSize += ref.AssetSize;
        _assetRefCounts[ref.Id] = incrementRefCount ? 1 : 0;

        LOG("Loaded asset id={}, size={} b, total={} Mb", ref.Id, ref.AssetSize, _loadedAssetsSize / 1024 / 1024.0)
    }

    template <typename T>
    void AssetManager::PostLoad(T& asset)
    {
        if constexpr (requires { asset.PostLoad(); })
        {
            asset.PostLoad();
        }
    }
}
