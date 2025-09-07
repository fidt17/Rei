#pragma once
#include "AssetRef.h"
#include "AssetsMap.h"
#include "Common/Time/ScopedTimer.h"
#include "Modules/Resources/AssetBuilder.h"
#include "Modules/Resources/Serialization/BinaryReader.h"

namespace rei::assets
{
    SET_LOG_SCOPE("Asset Manager")

    class AssetManager
    {
    public:
        explicit AssetManager();

        template <typename T>
        REI_API AssetRef<T> GetById(const std::string& id)
        {
            auto asset = AssetRef<T>(id);

            Load(asset);

            return asset;
        }

        template <typename T>
        REI_API AssetRef<T> GetByPath(const std::string& path)
        {
            AssetRef<T> ref(path);

            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset != _loadedAssets.end())
            {
                ref.Asset = ((AssetRef<T>*)loadedAsset->second)->Asset;
                ref.IsLoaded = true;
                return ref;
            }

            std::string filePath = path;
            if (filePath.rfind("@", 0) == 0)
            {
                filePath = filePath.substr(1, filePath.size() - 1);
            }

            const auto base_filename = filePath.substr(filePath.find_last_of("/\\") + 1);
            const auto dirPath = std::filesystem::temp_directory_path().string() + "Rei Engine\\";
            const auto dest = dirPath + base_filename + "_" + std::to_string(_tmpFiles.size()) + ".data";
            _tmpFiles.push_back(dest);

            std::filesystem::create_directory(dirPath);
            remove(dest.c_str());

            std::cout << "\n";
            LOG("Created temp file at {}", dest)

            i32 _ = resources::AssetBuilder().BuildAsset(filePath, dest, 0);

            i32 assetSize;
            ref.Asset = new T(Load<T>(dest, 0, assetSize));
            ref.IsLoaded = true;
            ref.AssetSize = assetSize;
            _loadedAssetsSize += assetSize;

            _loadedAssets[ref.Id] = new AssetRef<T>(ref);
            LOG("Loaded asset id={}, size={} Mb, total={} Mb", ref.Id, assetSize / 1024 / 1024.0, _loadedAssetsSize / 1024 / 1024.0)

            return ref;
        }

        template <typename T, typename... Args>
        REI_API AssetRef<T> CreateAssetWithId(std::string id, Args... args)
        {
            AssetRef<T>* asset = new AssetRef<T>(id);

            asset->Asset = new T(args...);
            asset->IsLoaded = true;
            _loadedAssets[asset->Id] = asset;

            return *asset;
        }

        template <typename T, typename... Args>
        REI_API AssetRef<T> CreateAsset(Args... args)
        {
            std::string id("runtime_asset_" + STRING(_runtimeAssetCounter++));
            return CreateAssetWithId<T>(id, args...);
        }

        template <typename T>
        REI_API bool Load(AssetRef<T>& ref)
        {
            if (ref.Id == "") return false;
            if (ref.IsLoaded) return true;

            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset != _loadedAssets.end())
            {
                ref.Asset = ((AssetRef<T>*)loadedAsset->second)->Asset;
                ref.IsLoaded = true;
                return true;
            }

            try
            {
                time::ScopedTimer timer(std::format("Asset {0} loading", ref.Id));

                // if an absolute path to the asset is used instead
                if (ref.Id.rfind("@", 0) == 0)
                {
                    ref = GetByPath<T>(ref.Id);
                    return true;
                }

                const auto assetInfo = _map->GetAssetInfo(ref.Id);

                Load(ref, assetInfo.Path, assetInfo.Offset);
                return true;
            }
            catch (std::exception e)
            {
                LOG_ERROR("Cought exception while trying to load asset id={}\n Exception: {}", ref.Id, e.what())
                ref.IsLoaded = false;
            }

            return false;
        }

        REI_API void UnloadAllAssets()
        {
            for (auto loadedAsset : _loadedAssets)
            {
                LOG("Delete asset id={}", loadedAsset.first)
                _loadedAssetsSize -= loadedAsset.second->GetAssetSize();

                loadedAsset.second->UnloadAsset();
                delete loadedAsset.second;
            }
        }

        void DeleteTmpFiles() const
        {
            for (const auto& tmpFile : _tmpFiles)
            {
                LOG_WARNING("Deleted temp file at {}", tmpFile)
                remove(tmpFile.c_str());
            }
        }

    private:
        std::unique_ptr<AssetsMap> _map;

        u32 _runtimeAssetCounter = 0;
        i64 _loadedAssetsSize = 0;
        std::unordered_map<std::string, IAssetRef*> _loadedAssets{};

        std::vector<std::string> _tmpFiles;

        template <typename T>
        T Load(const std::string& path, const i32 offset)
        {
            i32 size;
            return Load<T>(path, offset, size);
        }

        template <typename T>
        T Load(const std::string& path, const i32 offset, i32& size)
        {
            auto reader = resources::BinaryReader(path, offset);
            auto asset = reader.Get<T>();

            size = reader.GetPosition() - offset;

            reader.Close();

            return asset;
        }

        template <typename T>
        void Load(AssetRef<T>& ref, const std::string& path, const i32 offset)
        {
            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset != _loadedAssets.end())
            {
                ref.Asset = ((AssetRef<T>*)loadedAsset->second)->Asset;
                ref.IsLoaded = true;
                return;
            }

            i32 assetSize;
            auto asset = Load<T>(path, offset, assetSize);

            _loadedAssets[ref.Id] = new AssetRef<T>(ref);
            ref.Asset = new T(asset);
            ref.IsLoaded = true;
            ref.AssetSize = assetSize;

            _loadedAssetsSize += assetSize;

            LOG("Loaded asset id={}, size={} b, total={} Mb", ref.Id, assetSize, _loadedAssetsSize / 1024 / 1024.0)
        }
    };
}
