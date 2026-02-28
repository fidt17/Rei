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

    template <typename T>
    bool FinalizeSceneDependency(const scenes::SceneAssetPreloader& preloader, const std::string& id);

    struct AssetDependency
    {
        std::string Id;
        std::function<void(const scenes::SceneAssetPreloader&)> LoadData;
        std::function<void(const scenes::SceneAssetPreloader&)> PostLoad;
    };

    template <typename T>
    AssetDependency CreateTypedAssetDependency(const std::string& id)
    {
        return {
            .Id = id,
            .LoadData = [id](const scenes::SceneAssetPreloader& preloader)
            {
                PreloadSceneDependency<T>(preloader, id);
            },
            .PostLoad = [id](const scenes::SceneAssetPreloader& preloader)
            {
                FinalizeSceneDependency<T>(preloader, id);
            },
        };
    }
}
