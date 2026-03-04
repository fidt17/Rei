#include "pch.h"
#include "SceneAssetPreloader.h"

namespace rei::scenes
{
    namespace
    {
        std::string JoinFailedDependencyIds(const std::vector<std::string>& dependencyIds)
        {
            std::ostringstream stream;
            for (std::size_t i = 0; i < dependencyIds.size(); ++i)
            {
                if (i > 0)
                {
                    stream << ", ";
                }

                stream << dependencyIds[i];
            }

            return stream.str();
        }
    }

    SceneAssetPreloader::SceneAssetPreloader(const std::shared_ptr<assets::AssetManager>& assetManager)
        : _assetManager(assetManager) { }

    bool SceneAssetPreloader::Preload(const std::vector<assets::AssetDependency>& dependencies) const
    {
        const auto uniqueDependencies = GetUniqueDependencies(dependencies);
        if (uniqueDependencies.empty())
        {
            LOG_DEBUG("Scene asset dependencies loaded: 0 | ids=[]")
            return true;
        }
        
        std::vector<std::future<void>> loadFutures;
        const u32 hardwareConcurrency = std::thread::hardware_concurrency();
        const std::size_t suggestedWorkerCount = std::max(1u, hardwareConcurrency);
        const std::size_t workerCount = std::min<std::size_t>(suggestedWorkerCount, uniqueDependencies.size());
        loadFutures.reserve(workerCount);
        
        LOG_D(std::format("workersThreads={}, dependenciesCount={}, ids=[{}]", workerCount, dependencies.size(), JoinDependencyIds(dependencies)), "Loading scene dependencies")

        std::atomic<std::size_t> nextDependencyIndex = 0;
        std::atomic hasDependencyLoadFailure = false;
        std::mutex failedDependencyIdsMutex;
        std::vector<std::string> failedDependencyIds;
        failedDependencyIds.reserve(uniqueDependencies.size());

        for (std::size_t i = 0; i < workerCount; ++i)
        {
            loadFutures.push_back(std::async(std::launch::async, [this, &uniqueDependencies, &nextDependencyIndex, &hasDependencyLoadFailure, &failedDependencyIdsMutex, &failedDependencyIds]
            {
                const assets::AssetPostLoadHandler::ScopedPostLoadSuppression postLoadSuppression(true);

                while (true)
                {
                    const std::size_t index = nextDependencyIndex.fetch_add(1);
                    if (index >= uniqueDependencies.size()) break;

                    const auto& dependency = uniqueDependencies[index];
                    if (dependency.LoadData(*this)) continue;

                    hasDependencyLoadFailure.store(true);
                    std::scoped_lock lock(failedDependencyIdsMutex);
                    failedDependencyIds.push_back(dependency.Id);
                }
            }));
        }

        for (auto& loadFuture : loadFutures)
        {
            loadFuture.get();
        }

        const bool didFlushDeferredPostLoads = _assetManager->FlushDeferredPostLoads();
        if (!didFlushDeferredPostLoads)
        {
            LOG_ERROR("Failed to run one or more deferred post-load actions while preloading scene dependencies")
        }

        if (hasDependencyLoadFailure.load())
        {
            std::sort(failedDependencyIds.begin(), failedDependencyIds.end());
            failedDependencyIds.erase(std::unique(failedDependencyIds.begin(), failedDependencyIds.end()), failedDependencyIds.end());
            LOG_ERROR("Failed to preload scene asset dependencies count={} | ids=[{}]", failedDependencyIds.size(), JoinFailedDependencyIds(failedDependencyIds))
        }

        LOG_DEBUG("Scene asset dependencies loaded: {} | ids=[{}]", uniqueDependencies.size(), JoinDependencyIds(uniqueDependencies))
        return didFlushDeferredPostLoads && !hasDependencyLoadFailure.load();
    }

    std::string SceneAssetPreloader::JoinDependencyIds(const std::vector<assets::AssetDependency>& dependencies)
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

    std::vector<assets::AssetDependency> SceneAssetPreloader::GetUniqueDependencies(const std::vector<assets::AssetDependency>& dependencies)
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
}
