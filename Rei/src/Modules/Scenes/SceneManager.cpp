#include "pch.h"
#include "SceneManager.h"

#include "BuildScenesConfig.h"
#include "Scene.h"

#include <unordered_set>
#include "Modules/Assets/Core/AssetManager.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::scenes
{
    class Scene;

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
        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            _entityManager->Create(sceneEntity);
        }
        GetInternalWorld()->RefreshAll();
        
        _entityManager->ResolveDependencies();
        _entityManager->ResolveTransformParents();
    }

    bool SceneManager::LoadScene(const i32 id)
    {
        UnloadCurrentScene();
        try
        {
            REI_THROW_IF(!_buildScenesConfig.IsLoaded(), "Build Scenes Config is not loaded")
            REI_THROW_IF(!_buildScenesConfig->Has(id), "Scene with id [" + STRING(id) + "] is missing from build scenes")

            _activeScene.Id = _buildScenesConfig->GetScene(id).Id;
            if (_assetManager->Load(_activeScene) && _sceneAssetPreloader.Preload(CollectSceneAssetDependencies()))
            {
                CreateSceneEntities();
                LOG("Loaded scene {}", _activeScene->GetName())
                return true;
            }
        }
        catch (const std::exception& exception)
        {
            LOG_ERROR("Failed to load scene {}. {}", id, exception.what())
        }
        catch (...)
        {
            LOG_ERROR("Failed to load scene {}. Unknown exception", id)
        }

        UnloadCurrentScene();
        return false;
    }

    void SceneManager::UnloadCurrentScene()
    {
        _entityManager->DestroySceneEntities();
        if (_activeScene.IsLoaded()) _assetManager->Release(_activeScene);
        _activeScene.Id.clear();
        _activeScene.Record.reset();
    }

    void SceneManager::Shutdown()
    {
        UnloadCurrentScene();
        if (_buildScenesConfig.IsLoaded()) _assetManager->Release(_buildScenesConfig);
        _buildScenesConfig.Id.clear();
        _buildScenesConfig.Record.reset();
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
