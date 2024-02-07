#include "pch.h"
#include "EntityManager.h"

#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Components/EntityInfo.h"

rei::EntityManager::EntityManager(const std::shared_ptr<ecs::World>& world)
    : _ecs(world->GetRegistry()),
      _entityInfoFilter(world->GetFiltersRegistry()->Get<EntityInfo>())
{
}

rei::ecs::Entity rei::EntityManager::GetBySceneId(const i32 id) const
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

void rei::EntityManager::Create(const SceneEntity& sceneEntity) const
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

            AddComponent(e, behaviourId, serializedData, false);
            behavioursToInit.push_back({e, behaviourId});
        }
    }
    catch (std::exception& e)
    {
        LOG_ERROR("Scene entity creation exception. Entity Id " + STRING(sceneEntity.GetId()) + ". Exception: " + e.what());
    }

    for (const auto& [Entity, BehaviourId] : behavioursToInit)
    {
        auto& b = GetComponent(Entity, BehaviourId);
        InitBehaviour(Entity, b);
    }
}

rei::Behaviour& rei::EntityManager::GetComponent(const ecs::Entity e, const i32 componentId) const
{
    return _behaviourRegistry.GetBehaviour(e, componentId);
}

rei::Behaviour& rei::EntityManager::AddComponent(const ecs::Entity e, const i32 componentId, const nlohmann::json& data, const bool init) const
{
    auto& b = _behaviourRegistry.AddBehaviour(e, componentId, data);

    GET(e, BehaviourCollection).Behaviours.push_back(componentId);

    if (init)
    {
        InitBehaviour(e, b);
    }

    return b;
}

void rei::EntityManager::Destroy(const ecs::Entity e) const
{
    for (const auto behaviour : GET(e, BehaviourCollection).Behaviours)
    {
        GetComponent(e, behaviour).Dispose();
    }
    DESTROY_ENTITY(e);
}

void rei::EntityManager::InitBehaviour(const ecs::Entity e, Behaviour& b) const
{
    b.Init();
    GET(e, StartBehavioursEvent).Behaviours.push_back(b.GetId());
}
