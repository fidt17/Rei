#include "pch.h"
#include "SceneManager.h"

#include "BuildScenesConfig.h"
#include "Scene.h"
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
        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            _entityManager->Create(sceneEntity);
        }
        GetInternalWorld()->RefreshAll();
        
        _entityManager->ResolveTransformParents();
    }

    void SceneManager::LoadScene(const i32 id)
    {
        REI_ASSERT(_buildScenesConfig.IsLoaded(), "Build Scenes Config is not loaded")
        REI_THROW_IF(!_buildScenesConfig->Has(id), "Scene with id [" + STRING(id) + "] is missing from build scenes")

        _activeScene = _buildScenesConfig->GetScene(id);
        _assetManager->Load(_activeScene);

        _sceneAssetPreloader.Preload(CollectSceneAssetDependencies());

        CreateSceneEntities();

        LOG("Loaded scene {}", _activeScene->GetName())
    }

    void SceneManager::UnloadCurrentScene()
    {
        const auto roots = _entityManager->GetRootEntities();
        for (const auto root : roots)
        {
            _entityManager->Destroy(root);
        }
        GetInternalWorld()->Refresh();

        _assetManager->Release(_activeScene);
    }

    void SceneManager::Shutdown()
    {
        UnloadCurrentScene();
        _assetManager->Release(_buildScenesConfig);
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
