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

        _activeScene = std::make_shared<Scene>(GetAssetManager().Load<Scene>(sceneRef));

        for (const auto& sceneEntity : _activeScene->GetEntities())
        {
            CreateSceneEntity(sceneEntity);
        }

        LOG("Loaded scene \"" + _activeScene->GetName() + "\"")
    }

    void SceneManager::CreateSceneEntity(const SceneEntity& sceneEntity)
    {
        ECS_WORLD(GetInternalWorld());

        struct EntityToBehaviour
        {
            ecs::Entity Entity;
            i32 BehaviourId;
        };
        auto behavioursToInit = std::vector<EntityToBehaviour>();

        try
        {
            const auto e = NEW_ENTITY();
            GET(e, EntityInfo) = {sceneEntity.GetId(), sceneEntity.GetName()};
            auto& behavioursToAdd = sceneEntity.GetBehaviours();

            for (auto behaviourData : behavioursToAdd)
            {
                const i32 behaviourId = behaviourData.at("Id");

                const std::string SERIALIZE_DATA = "SerializedData";
                nlohmann::json serializedData;
                if (behaviourData.contains(SERIALIZE_DATA))
                {
                    serializedData = behaviourData.at(SERIALIZE_DATA);
                }

                GetEntityManager().AddBehaviour(e, behaviourId, serializedData, false);
                behavioursToInit.push_back({e, behaviourId});
            }
        }
        catch (std::exception& e)
        {
            LOG_ERROR("Scene entity creation exception. Entity Id " + STRING(sceneEntity.GetId()) + ". Exception: " + e.what());
        }

        for (const auto& [Entity, BehaviourId] : behavioursToInit)
        {
            auto& b = GetEntityManager().GetBehaviour(Entity, BehaviourId);
            GetEntityManager().InitBehaviour(Entity, b);
        }
    }
}
