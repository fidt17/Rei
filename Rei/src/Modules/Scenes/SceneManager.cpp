#include "pch.h"
#include "SceneManager.h"

#include "BuildScenesConfig.h"
#include "Scene.h"
#include "Engine/Services.h"
#include "Modules/Assets/AssetManager.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::scenes
{
    class Scene;

    SceneManager::SceneManager(const std::shared_ptr<EntityManager> entityManager)
        : _buildScenesConfig(GetAssetManager().LoadById<BuildScenesConfig>("0")),
          _entityManager(entityManager)
    {
    }

    void SceneManager::LoadScene(const int id)
    {
        REI_THROW_IF(_activeScene, "Another scene is active")
        REI_THROW_IF(!_buildScenesConfig.Has(id), "Scene with id [" + STRING(id) + "] is missing from build scenes")

        const auto& sceneRef = _buildScenesConfig.GetScene(id);

        _activeScene = std::make_shared<Scene>(GetAssetManager().Load<Scene>(sceneRef));

        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            _entityManager->Create(sceneEntity);
        }

        LOG("Loaded scene \"" + _activeScene->GetName() + "\"")
    }
}
