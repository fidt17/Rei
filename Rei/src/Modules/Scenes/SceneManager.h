#pragma once
#include "BuildScenesConfig.h"
#include "Scene.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::scenes
{
    class SceneManager
    {
    public:
        explicit SceneManager(const std::shared_ptr<assets::AssetManager>& assetManager, const std::shared_ptr<EntityManager>& entityManager);
    
        void LoadScene(int id);
        
    private:
        assets::AssetRef<BuildScenesConfig> _buildScenesConfig;

        std::shared_ptr<assets::AssetManager> _assetManager;
        assets::AssetRef<Scene> _activeScene;
        std::shared_ptr<EntityManager> _entityManager;
    };
}
