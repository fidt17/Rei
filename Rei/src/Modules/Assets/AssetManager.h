#pragma once
#include "AssetRef.h"
#include "AssetsMap.h"
#include "Modules/Resources/AssetBuilder.h"
#include "Modules/Resources/Serialization/BinaryReader.h"

namespace rei::assets
{
    class AssetManager
    {
    public:
        explicit AssetManager();

        template <typename T>
        REI_API AssetRef<T> GetById(const std::string& id)
        {
            auto asset = AssetRef<T>(AssetId(id));

            Load(asset);
            
            return asset;
        }

        template <typename T>
        AssetRef<T> GetByPath(const std::string& path)
        {
            AssetRef<T> ref(path);

            auto loadedAsset = _loadedAssets.find(ref.Id);
            if (loadedAsset != _loadedAssets.end())
            {
                ref.Asset = ((AssetRef<T>*)loadedAsset->second)->Asset;
                ref.IsLoaded = true;
                return ref;
            }
            
            const auto base_filename = path.substr(path.find_last_of("/\\") + 1);
            const auto dirPath = std::filesystem::temp_directory_path().string() + "Rei Engine\\";
            const auto dest = dirPath + base_filename + "_" + std::to_string(_tmpFiles.size()) + ".data";
            _tmpFiles.push_back(dest);

            std::filesystem::create_directory(dirPath);
            remove(dest.c_str());

            resources::AssetBuilder builder;
            i32 _ = builder.BuildAsset(path, dest, 0);

            LOG_WARNING("Created temp file at " + dest);

            ref.Asset = new T(Load<T>(dest, 0));
            ref.IsLoaded = true;

            _loadedAssets[ref.Id] = new AssetRef<T>(ref);

            return ref;
        }

        template <typename T>
        REI_API void Load(AssetRef<T>& ref)
        {
            if (ref.IsLoaded) return;
            
            const auto assetInfo = _map->GetAssetInfo(ref.Id);

            Load(ref, assetInfo.Path, assetInfo.Offset);
        }

        REI_API void UnloadAllAssets()
        {
            for (auto loadedAsset : _loadedAssets)
            {
                LOG("Delete: " + loadedAsset.first)
                delete loadedAsset.second;
            }
        }

        void DeleteTmpFiles() const
        {
            for (const auto& tmpFile : _tmpFiles)
            {
                LOG_WARNING("Deleted temp file at " + tmpFile);
                remove(tmpFile.c_str());
            }
        }

    private:
        std::unique_ptr<AssetsMap> _map;
        std::unordered_map<AssetId, IAssetRef*> _loadedAssets;

        std::vector<std::string> _tmpFiles;

        template <typename T>
        T Load(const std::string& path, const i32 offset)
        {
            auto reader = resources::BinaryReader(path, offset);
            auto asset = reader.Get<T>();
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
            
            auto asset = Load<T>(path, offset);

            _loadedAssets[ref.Id] = new AssetRef<T>(ref);
            
            ref.Asset = new T(asset);
            ref.IsLoaded = true;
        }
    };
}
