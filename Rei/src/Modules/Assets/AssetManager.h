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
        explicit AssetManager(const std::string& resourcesPath);

        template <typename T>
        REI_API T Load(const std::string& path, const i64 offset) const
        {
            auto reader = resources::BinaryReader(path, offset);
            auto t = reader.Get<T>();
            reader.Close();
            return t;
        }

        template <typename T>
        REI_API T LoadById(const std::string& id) const
        {
            return Load<T>(AssetRef(AssetId(id)));
        }

        template <typename T>
        REI_API T Load(const AssetRef ref) const
        {
            REI_ASSERT_NOT_NULL(_map)

            const auto assetInfo = _map->GetAssetInfo(ref.AssetId.Id);
            return Load<T>(assetInfo.Path, assetInfo.Offset);
        }
        
        template <typename T>
        T LoadFrom(const std::string& path)
        {
            const auto base_filename = path.substr(path.find_last_of("/\\") + 1);
            const auto dirPath = std::filesystem::temp_directory_path().string() + "Rei Engine\\";
            const auto dest = dirPath + base_filename + "_" + std::to_string(_tmpFiles.size()) + ".data";
            _tmpFiles.push_back(dest);

            std::filesystem::create_directory(dirPath);
            remove(dest.c_str());

            resources::AssetBuilder builder;
            i32 _ = builder.BuildAsset(path, dest, 0);

            LOG_WARNING("Created temp file at " + dest);

            return Load<T>(dest, 0);
        }
        
        void DeleteTmpFiles()
        {
            for (auto tmpFile : _tmpFiles)
            {
                LOG_WARNING("Deleted temp file at " + tmpFile);
                remove(tmpFile.c_str());
            }
        }

    private:
        std::unique_ptr<AssetsMap> _map;

        std::vector<std::string> _tmpFiles;
    };
}
