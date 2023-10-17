#pragma once
#include <filesystem>

#include "BinaryReader.h"
#include "AssetRef.h"
#include "AssetsMap.h"

namespace rei::assets
{

    class AssetManager
    {
    public:
        explicit AssetManager(const std::string& resourcesPath);

        template <typename T>
        REI_API T Load(const std::string& path, const i64 offset) const
        {
            auto reader = BinaryReader(path);
            reader.SetPosition(offset);
            return reader.Get<T>();
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

    private:
        std::unique_ptr<AssetsMap> _map;
    };
}
