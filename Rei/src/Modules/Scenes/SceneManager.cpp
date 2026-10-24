#include "pch.h"
#include "SceneManager.h"

#include "BuildScenesConfig.h"
#include "Scene.h"

#include <unordered_set>
#include "Modules/Assets/Core/AssetManager.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::scenes
{
    SceneManager::SceneManager(const std::shared_ptr<assets::AssetManager>& assetManager, const std::shared_ptr<EntityManager>& entityManager)
        : _buildScenesConfig(assetManager->GetById<BuildScenesConfig>("0")),
          _assetManager(assetManager),
          _sceneAssetPreloader(assetManager),
          _entityManager(entityManager)
    {
    }

    void SceneManager::CreateSceneEntities()
    {
        REI_THROW_IF(!_activeScene.IsLoaded(), "Active scene is not loaded")
        
        LOG_D(std::format("count={}", STRING(_activeScene->GetEntities().size())), "Creating scene entities")
        std::unordered_set<i32> entityIds;
        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            REI_THROW_IF(!entityIds.insert(sceneEntity.GetId()).second, "Duplicate scene entity id: " + STRING(sceneEntity.GetId()))
        }
        const u64 totalWork = _activeScene->GetEntities().size() + 4;
        u64 completed = 0;
        const auto reportCompleted = [&] { ReportLoadProgress(80 + static_cast<i32>(20 * ++completed / totalWork)); };
        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            _entityManager->Create(sceneEntity);
            reportCompleted();
        }
        GetInternalWorld()->RefreshAll();
        reportCompleted();
        _entityManager->ResolveDependencies();
        reportCompleted();
        _entityManager->ResolveTransformParents();
        reportCompleted();
    }

    std::string SceneManager::GetActiveSceneName() const
    {
        return _activeScene.IsLoaded() ? _activeScene->GetName() : "";
    }

    bool SceneManager::LoadScene(const std::string& assetId)
    {
        return LoadScene([&] { return _assetManager->GetById<Scene>(assetId); });
    }

    bool SceneManager::LoadScene(const i32 id)
    {
        return LoadScene([&]
        {
            REI_THROW_IF(!_buildScenesConfig.IsLoaded(), "Build Scenes Config is not loaded")
            REI_THROW_IF(!_buildScenesConfig->Has(id), "Scene with id [" + STRING(id) + "] is missing from build scenes")
            return _assetManager->GetById<Scene>(_buildScenesConfig->GetScene(id).Id);
        });
    }

    bool SceneManager::LoadScene(const std::string& assetId, const nlohmann::json& sceneData)
    {
        return LoadScene([&]
        {
            REI_THROW_IF(assetId.empty(), "Scene asset ID is missing")
            return _assetManager->CreateAssetWithId<Scene>(assetId, sceneData);
        });
    }

    void SceneManager::ReportLoadProgress(const i32 percent)
    {
        if (_loadProgressPercent.exchange(percent) == percent) return;
        LoadProgressEvent(percent);
    }

    bool SceneManager::LoadScene(const std::function<assets::AssetRef<Scene>()>& load)
    {
        // Five work phases: unload, scene data, asset reads, GPU post-load, entities.
        _loadProgressPercent.store(0);
        LoadProgressEvent(0);
        UnloadCurrentScene();
        ReportLoadProgress(20);
        try
        {
            _activeScene = load();
            if (_activeScene.IsLoaded())
            {
                const auto dependencies = CollectSceneAssetDependencies();
                ReportLoadProgress(40);
                if (_sceneAssetPreloader.Preload(dependencies, [this](const f32 progress) { ReportLoadProgress(40 + static_cast<i32>(40 * progress)); }))
                {
                    CreateSceneEntities();
                    LOG("Loaded scene {}", _activeScene->GetName())
                    ReportLoadProgress(100);
                    return true;
                }
            }
        }
        catch (const std::exception& exception)
        {
            LOG_ERROR("Failed to load scene. {}", exception.what())
        }
        catch (...)
        {
            LOG_ERROR("Failed to load scene. Unknown exception")
        }

        UnloadCurrentScene();
        return false;
    }

    void SceneManager::UnloadCurrentScene()
    {
        _entityManager->DestroySceneEntities();
        _activeScene = {};
    }

    void SceneManager::Shutdown()
    {
        UnloadCurrentScene();
        _buildScenesConfig = {};
    }

    std::vector<assets::AssetDependency> SceneManager::CollectSceneAssetDependencies()
    {
        std::vector<assets::AssetDependency> dependencies{};

        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            for (const auto& behaviourData : sceneEntity.GetBehaviours())
            {
                if (!behaviourData.contains("Id")) continue;

                const i32 behaviourId = behaviourData.at("Id");
                if (!behaviourData.contains("SerializedData")) continue;

                const auto& serializedData = behaviourData.at("SerializedData");
                _entityManager->GetBehaviourRegistry().CollectAssetDependencies(behaviourId, serializedData, dependencies);
            }
        }

        return dependencies;
    }
}
