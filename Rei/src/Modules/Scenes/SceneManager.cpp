#include "pch.h"
#include "SceneManager.h"

#include "BuildScenesConfig.h"
#include "Scene.h"
#include "Engine/Services.h"
#include "Modules/Assets/AssetManager.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::scenes
{
    class Scene;

    SceneManager::SceneManager()
        : _buildScenesConfig(GetAssetManager().LoadById<BuildScenesConfig>("0"))
    {
    }

    void SceneManager::LoadScene(const int id)
    {
        REI_THROW_IF(_activeScene, "Another scene is active")
        REI_THROW_IF(!_buildScenesConfig.Has(id), "Scene with id [" + STRING(id) + "] is missing from build scenes")

        const auto& sceneRef = _buildScenesConfig.GetScene(id);
        LOG(" \nLoading scene ["+STRING(id)+"]. Ref: ["+sceneRef.AssetId.Id+"]")

        _activeScene = std::make_shared<Scene>(GetAssetManager().Load<Scene>(sceneRef));
        LOG("Scene name: " + _activeScene->GetName())

        CreateSceneEntities();
    }

    void SceneManager::CreateSceneEntities()
    {
        REI_THROW_IF(!_activeScene, "Active scene is missing")

        LOG("Creating scene entities. Entities count: " + std::to_string(_activeScene->GetEntities().size()))

        ECS_WORLD(GetInternalWorld());

        for (auto sceneEntity : _activeScene->GetEntities())
        {
            auto e = NEW_ENTITY();
            GET(e, EntityInfo) = {sceneEntity.GetId(), sceneEntity.GetName()};
            LOG("New Entity (" + STRING(sceneEntity.GetId()) + ", " + sceneEntity.GetName() + ")");

            for (auto behaviourData : sceneEntity.GetBehaviours())
            {
                auto& b = GetEntityManager().AddBehaviour(e, behaviourData.at("Id"), behaviourData.at("SerializedData"));
                b.Init();
            }
        }
    }
}
