#pragma once
#include "BuildScenesConfig.h"
#include "Scene.h"

namespace rei::scenes
{
    class SceneManager
    {
    public:
        explicit SceneManager();
    
        void LoadScene(int id);

        void CreateSceneEntity(const SceneEntity& sceneEntity);

    private:
        BuildScenesConfig _buildScenesConfig;

        std::shared_ptr<Scene> _activeScene;
    };
}
