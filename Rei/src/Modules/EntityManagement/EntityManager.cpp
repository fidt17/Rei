#include "pch.h"
#include "EntityManager.h"

#include "rei_behaviours/transformation/Transform.h"
#include "Common/Time/ScopedTimer.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Scenes/SceneManager.h"

namespace rei
{
    EntityManager::EntityManager(const std::shared_ptr<ecs::World>& world)
        : _ecs(world->GetRegistry()),
          _entityInfoFilter(world->GetFiltersRegistry()->Get<EntityInfo>())
    {
    }

    ecs::Entity EntityManager::GetBySceneId(const i32 id) const
    {
        FOR(e, _entityInfoFilter)
        {
            if (GET(e, EntityInfo).Id == id)
            {
                return e;
            }
        }

        return ecs::NULL_ENTITY;
    }

    ecs::Entity EntityManager::CreateNewEntity(const std::string& name)
    {
        ECS_WORLD(GetInternalWorld());

        const auto e = NEW_ENTITY();
        GET(e, EntityInfo) = {GenerateNewSceneEntityId(), name};

        AddBehaviour<Transform>(e).Reset();

        return e;
    }

    void EntityManager::Create(const SceneEntity& sceneEntity) const
    {
        time::ScopedTimer entityCreationTimer("Entity " + STRING(sceneEntity.GetId()) + ", " + sceneEntity.GetName() + " creation");
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

                AddBehaviour(e, behaviourId, serializedData, false);
                behavioursToInit.push_back({e, behaviourId});
            }
        }
        catch (std::exception& e)
        {
            LOG_ERROR("Scene entity creation exception. Entity Id {}. Exception: {}", sceneEntity.GetId(), e.what());
        }

        for (const auto& [Entity, BehaviourId] : behavioursToInit)
        {
            auto& b = GetBehaviour(Entity, BehaviourId);
            InitBehaviour(Entity, b);
        }
    }

    Behaviour& EntityManager::GetBehaviour(const ecs::Entity e, const i32 behaviourId) const
    {
        return _behaviourRegistry.GetBehaviour(e, behaviourId);
    }

    Behaviour& EntityManager::AddBehaviour(const ecs::Entity e, const i32 componentId, const nlohmann::json& data, const bool init) const
    {
        auto& b = _behaviourRegistry.AddBehaviour(e, componentId, data);

        if (init)
        {
            InitBehaviour(e, b);
        }

        return b;
    }

    void EntityManager::DeleteBehaviour(ecs::Entity e, i32 behaviourId)
    {
        GetBehaviourRegistry().GetBehaviour(e, behaviourId).Dispose();
        GetBehaviourRegistry().DeleteBehaviour(e, behaviourId);
    }

    void EntityManager::Destroy(const ecs::Entity e) const
    {
        for (const auto behaviour : GET(e, BehaviourCollection).Behaviours)
        {
            GetBehaviour(e, behaviour).Dispose();
        }
        DESTROY_ENTITY(e);
    }

    void EntityManager::InitBehaviour(const ecs::Entity e, Behaviour& b) const
    {
        b.LoadAssets(GetAssetManager());
        b.Init();

        GET(e, StartBehavioursEvent).Behaviours.push_back(b.GetBehaviourId());
    }

    i32 EntityManager::GenerateNewSceneEntityId() const
    {
        i32 maxId = -1;
        FOR(e, _entityInfoFilter)
        {
            const i32 id = GET(e, EntityInfo).Id;
            if (id > maxId)
            {
                maxId = id;
            }
        }

        return maxId + 1;
    }
}
