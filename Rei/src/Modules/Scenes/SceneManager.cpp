#include "pch.h"
#include "SceneManager.h"

#include "BuildScenesConfig.h"
#include "Scene.h"
#include "Common/Time/ScopedTimer.h"
#include "Modules/Assets/AssetManager.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::scenes
{
    class Scene;

    SceneManager::SceneManager(const std::shared_ptr<assets::AssetManager>& assetManager, const std::shared_ptr<EntityManager>& entityManager)
        : _buildScenesConfig(assetManager->GetById<BuildScenesConfig>("0")),
          _assetManager(assetManager),
          _entityManager(entityManager)
    {
    }

    void SceneManager::LoadScene(const int id)
    {
        LOG("Loading scene {}", id);
        REI_ASSERT(_buildScenesConfig.IsLoaded(), "Build Scenes Config is not loaded")
        
        time::ScopedTimer timer("Scene " + STRING(id) + " loading");
        REI_THROW_IF(!_buildScenesConfig->Has(id), "Scene with id [" + STRING(id) + "] is missing from build scenes")

        _activeScene = _buildScenesConfig->GetScene(id);
        _assetManager->Load(_activeScene);

        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            _entityManager->Create(sceneEntity);
        }
        GetInternalWorld()->RefreshAll();
        
        _entityManager->ResolveTransformParents();

        LOG("Loaded scene {}", _activeScene->GetName())
    }
}
