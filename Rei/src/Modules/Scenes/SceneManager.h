#pragma once

#include "BuildScenesConfig.h"
#include "Scene.h"
#include "SceneAssetPreloader.h"
#include "Modules/Assets/Core/AssetDependency.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::scenes
{
    class SceneManager
    {
    public:
        explicit SceneManager(const std::shared_ptr<assets::AssetManager>& assetManager, const std::shared_ptr<EntityManager>& entityManager);

        REI_API bool LoadScene(const std::string& assetId);
        REI_API bool LoadScene(i32 id);
        REI_API bool LoadScene(const std::string& assetId, const nlohmann::json& sceneData);
        REI_API const std::string& GetActiveSceneId() const { return _activeScene.Id; }
        REI_API std::string GetActiveSceneName() const;
        REI_API void UnloadCurrentScene();
        void Shutdown();
        
    private:
        std::vector<assets::AssetDependency> CollectSceneAssetDependencies();

        assets::AssetRef<BuildScenesConfig> _buildScenesConfig;

        std::shared_ptr<assets::AssetManager> _assetManager;
        SceneAssetPreloader _sceneAssetPreloader;
        assets::AssetRef<Scene> _activeScene;
        std::shared_ptr<EntityManager> _entityManager;
        
        void CreateSceneEntities();
        bool LoadScene(const std::function<assets::AssetRef<Scene>()>& load);
    };
}
