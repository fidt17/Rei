#pragma once
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/EntityManagement/EntityManager.h"

REI_EXTERN_API inline void GetEntityData(const i32 sceneEntityId, char* outputBuffer, const int bufferSize)
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
    if (IS_DEAD(e)) return;

    nlohmann::json data;
    data["EntityId"] = e.Id;
    data["EntityGeneration"] = e.Generation;

    const auto& entityInfo = GET(e, EntityInfo);
    data["SceneId"] = entityInfo.Id;
    data["Name"] = entityInfo.Name;

    const auto& behaviourCollection = GET(e, BehaviourCollection);
    data["Behaviours"] = nlohmann::json::array();

    for (const auto behaviour : behaviourCollection.Behaviours)
    {
        data["Behaviours"].push_back(rei::GetEntityManager().GetBehaviourRegistry().GetBehaviourData(e, behaviour));
    }

    strncpy_s(outputBuffer, bufferSize, data.dump().c_str(), _TRUNCATE);
}

REI_EXTERN_API inline void RenameEntity(const i32 sceneEntityId, const char* newName)
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
    if (IS_DEAD(e)) return;

    GET(e, EntityInfo).Name = newName;
}

REI_EXTERN_API inline void SetEntityData(const char* json)
{
    ECS_WORLD(rei::GetInternalWorld());

    nlohmann::json data = nlohmann::json::parse(json);
    const i32 sceneEntityId = data.at("SceneId");
    const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
    if (IS_DEAD(e)) return;

    for (auto b : data.at("Behaviours"))
    {
        const i32 behaviourId = b.at("REI_BEHAVIOUR_ID");
        rei::GetEntityManager().GetBehaviourRegistry().SetBehaviourData(e, behaviourId, b);
    }
}

REI_EXTERN_API inline void AddBehaviour(const i32 sceneEntityId, const i32 behaviourId)
{
    rei::GetEngine().ExecuteOnMainThread([=]()
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        rei::GetEntityManager().AddBehaviour(e, behaviourId, {});
    });
}

REI_EXTERN_API inline void DeleteBehaviour(const i32 sceneEntityId, const i32 behaviourId)
{
    rei::GetEngine().ExecuteOnMainThread([=]()
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        rei::GetEntityManager().DeleteBehaviour(e, behaviourId);
    });
}
