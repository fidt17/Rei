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
        explicit SceneAssetPreloader(const std::shared_ptr<assets::AssetManager>& assetManager)
            : _assetManager(assetManager)
        {
        }

        void Preload(const std::vector<assets::AssetDependency>& dependencies) const
        {
            const auto uniqueDependencies = GetUniqueDependencies(dependencies);
            if (uniqueDependencies.empty())
            {
                LOG_DEBUG("Scene asset dependencies loaded: 0 | ids=[]")
                return;
            }

            std::vector<std::future<void>> loadFutures;
            const u32 hardwareConcurrency = std::thread::hardware_concurrency();
            const std::size_t suggestedWorkerCount = std::max(1u, hardwareConcurrency);
            const std::size_t workerCount = std::min<std::size_t>(suggestedWorkerCount, uniqueDependencies.size());
            loadFutures.reserve(workerCount);
        
            std::atomic<std::size_t> nextDependencyIndex = 0;
            for (std::size_t i = 0; i < workerCount; ++i)
            {
                loadFutures.push_back(std::async(std::launch::async, [this, &uniqueDependencies, &nextDependencyIndex]
                {
                    while (true)
                    {
                        const std::size_t index = nextDependencyIndex.fetch_add(1);
                        if (index >= uniqueDependencies.size())
                        {
                            break;
                        }

                        uniqueDependencies[index].LoadData(*this);
                    }
                }));
            }

            for (auto& loadFuture : loadFutures)
            {
                loadFuture.get();
            }

            for (const auto& dependency : uniqueDependencies)
            {
                dependency.PostLoad(*this);
            }

            LOG_DEBUG("Scene asset dependencies loaded: {} | ids=[{}]", uniqueDependencies.size(), JoinDependencyIds(uniqueDependencies))
        }

    private:
        static std::string JoinDependencyIds(const std::vector<assets::AssetDependency>& dependencies)
        {
            std::ostringstream stream;
            for (std::size_t i = 0; i < dependencies.size(); ++i)
            {
                if (i > 0)
                {
                    stream << ", ";
                }

                stream << dependencies[i].Id;
            }

            return stream.str();
        }

        static std::vector<assets::AssetDependency> GetUniqueDependencies(const std::vector<assets::AssetDependency>& dependencies)
        {
            std::unordered_map<std::string, std::size_t> seenIds;
            seenIds.reserve(dependencies.size());

            std::vector<assets::AssetDependency> uniqueDependencies;
            uniqueDependencies.reserve(dependencies.size());

            for (const auto& dependency : dependencies)
            {
                if (seenIds.find(dependency.Id) != seenIds.end())
                {
                    continue;
                }

                seenIds[dependency.Id] = uniqueDependencies.size();
                uniqueDependencies.push_back(dependency);
            }

            return uniqueDependencies;
        }

        std::shared_ptr<assets::AssetManager> _assetManager;

    public:
        template <typename T>
        bool PreloadById(const std::string& id) const
        {
            auto ref = assets::AssetRef<T>(id);
            return _assetManager->EnsureAssetDataLoaded(ref, false);
        }

        template <typename T>
        bool FinalizeById(const std::string& id) const
        {
            auto ref = assets::AssetRef<T>(id);
            return _assetManager->RunPostLoad(ref);
        }
    };
}

namespace rei::assets
{
    template <typename T>
    bool PreloadSceneDependency(const scenes::SceneAssetPreloader& preloader, const std::string& id)
    {
        return preloader.PreloadById<T>(id);
    }

    template <typename T>
    bool FinalizeSceneDependency(const scenes::SceneAssetPreloader& preloader, const std::string& id)
    {
        return preloader.FinalizeById<T>(id);
    }
}
