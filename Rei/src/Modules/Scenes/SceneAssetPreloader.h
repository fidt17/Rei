#pragma once

#include <algorithm>
#include <atomic>
#include <future>
#include <sstream>
#include <thread>
#include <unordered_map>
#include <vector>

#include "Modules/Assets/Core/AssetDependency.h"
#include "Modules/Assets/Core/AssetManager.h"

namespace rei::scenes
{
    class SceneAssetPreloader
    {
    public:
        explicit SceneAssetPreloader(const std::shared_ptr<assets::AssetManager>& assetManager);

        bool Preload(const std::vector<assets::AssetDependency>& dependencies) const;
        
        template <typename T>
        bool PreloadById(const std::string& id) const
        {
            auto ref = assets::AssetRef<T>(id);
            return _assetManager->LoadInternal(ref, false);
        }

    private:
        std::shared_ptr<assets::AssetManager> _assetManager;
        
        static std::string JoinDependencyIds(const std::vector<assets::AssetDependency>& dependencies);
        static std::vector<assets::AssetDependency> GetUniqueDependencies(const std::vector<assets::AssetDependency>& dependencies);

    };
}

namespace rei::assets
{
    template <typename T>
    bool PreloadSceneDependency(const scenes::SceneAssetPreloader& preloader, const std::string& id)
    {
        return preloader.PreloadById<T>(id);
    }
}
