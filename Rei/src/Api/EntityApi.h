#pragma once
#include "Modules/EntityManagement/EntityManager.h"

REI_EXTERN_API inline void GetEntityData(const i32 sceneEntityId, char* outputBuffer, const int bufferSize)
{
    const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
    if (e == rei::ecs::NULL_ENTITY) return;

    nlohmann::json data;
    data["EntityId"] = e.Id;
    data["EntityGeneration"] = e.Generation;

    ECS_WORLD(rei::GetInternalWorld());
    const auto& entityInfo = GET(e, EntityInfo);
    data["SceneId"] = entityInfo.Id;
    data["Name"] = entityInfo.Name;
    data["Behaviours"] = nlohmann::json::array();

    for (const auto behaviour : entityInfo.Behaviours)
    {
        data["Behaviours"].push_back(rei::GetEntityManager().GetBehaviourRegistry().GetBehaviourData(e, behaviour));
    }

    strncpy_s(outputBuffer, bufferSize, data.dump().c_str(), _TRUNCATE);
}

REI_EXTERN_API inline void RenameEntity(const i32 sceneEntityId, const char* newName)
{
    const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
    if (e == rei::ecs::NULL_ENTITY) return;

    ECS_WORLD(rei::GetInternalWorld());
    GET(e, EntityInfo).Name = newName;
}

REI_EXTERN_API inline void SetEntityData(const char* json)
{
    nlohmann::json data = nlohmann::json::parse(json);

    const i32 sceneEntityId = data.at("SceneId");
    const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
    if (e == rei::ecs::NULL_ENTITY) return;

    for (auto b : data.at("Behaviours"))
    {
        const i32 behaviourId = b.at("REI_BEHAVIOUR_ID");
        rei::GetEntityManager().GetBehaviourRegistry().SetBehaviourData(e, behaviourId, b);
    }
}
