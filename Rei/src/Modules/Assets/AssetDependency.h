#pragma once

#include <functional>
#include <string>

#include "AssetManager.h"

namespace rei::assets
{
    struct AssetDependency
    {
        std::string Id;
        std::function<void(AssetManager&)> LoadData;
        std::function<void(AssetManager&)> PostLoad;
    };

    template <typename T>
    AssetDependency CreateTypedAssetDependency(const std::string& id)
    {
        return {
            .Id = id,
            .LoadData = [id](AssetManager& assetManager)
            {
                auto ref = AssetRef<T>(id);
                assetManager.PreloadData(ref);
            },
            .PostLoad = [id](AssetManager& assetManager)
            {
                auto ref = AssetRef<T>(id);
                assetManager.PreloadPostLoad(ref);
            },
        };
    }
}
