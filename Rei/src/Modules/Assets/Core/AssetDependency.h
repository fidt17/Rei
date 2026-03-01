#pragma once

#include <functional>
#include <string>

namespace rei::scenes
{
    class SceneAssetPreloader;
}

namespace rei::assets
{
    template <typename T>
    bool PreloadSceneDependency(const scenes::SceneAssetPreloader& preloader, const std::string& id);

    struct AssetDependency
    {
        std::string Id;
        std::function<bool(const scenes::SceneAssetPreloader&)> LoadData;
    };

    template <typename T>
    AssetDependency CreateTypedAssetDependency(const std::string& id)
    {
        return {
            .Id = id,
            .LoadData = [id](const scenes::SceneAssetPreloader& preloader)
            {
                return PreloadSceneDependency<T>(preloader, id);
            },
        };
    }
}
