#pragma once
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "rei_behaviours/render/RenderOutlineTag.h"
#include "rei_behaviours/transformation/Transform.h"

REI_EXTERN_API inline void CreateNewEntity(const char* name)
{
    std::string nameStr = name;
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        rei::GetEntityManager().CreateNewEntity(nameStr);
    });
}

REI_EXTERN_API inline void DestroyEntity(const i32 sceneEntityId)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        rei::GetEntityManager().Destroy(e);
    });
}

REI_EXTERN_API inline void GetSceneEntitiesList(char* outputBuffer, const int bufferSize)
{
    ECS_WORLD(rei::GetInternalWorld());

    const auto& entityInfoFilter = FILTER(EntityInfo);

    nlohmann::json data;
    data["Entities"] = nlohmann::json::array();

    FOR(e, entityInfoFilter)
    {
        const auto& info = GET(e, EntityInfo);

        data["Entities"].push_back({
            {"Id", info.Id},
            {"IsSelected", HAS(e, rei::editor::SelectedTag)}
        });
    }

    strncpy_s(outputBuffer, bufferSize, data.dump().c_str(), _TRUNCATE);
}

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
        const auto& behaviourData = rei::GetEntityManager().GetBehaviourRegistry().GetBehaviourData(e, behaviour);
        data["Behaviours"].push_back(behaviourData);
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

REI_EXTERN_API inline void SetEntityParent(const i32 sceneEntityId, const i32 parentSceneEntityId, const i32 order)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        const auto& parent = parentSceneEntityId == 0
            ? rei::ecs::NULL_ENTITY
            : rei::GetEntityManager().GetBySceneId(parentSceneEntityId);

        if (!HAS(e, rei::Transform)) return;

        GET(e, rei::Transform).SetParent(parent, order);
    });
}

REI_EXTERN_API inline void InstantiateEntity(const char* json)
{
    const std::string jsonStr = json;

    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        nlohmann::json data = nlohmann::json::parse(jsonStr);
        const i32 sourceEntityId = data.at("SourceEntityId");
        const std::string requestedName = data.value("RequestedName", "");
        const bool includeChildren = data.value("IncludeChildren", true);

        const auto& sourceEntity = rei::GetEntityManager().GetBySceneId(sourceEntityId);
        if (IS_DEAD(sourceEntity)) return;

        rei::GetEntityManager().Instantiate(sourceEntity, requestedName, includeChildren);
    });
}

REI_EXTERN_API inline void SetEntityData(const char* json)
{
    const std::string jsonStr = json;
    
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        nlohmann::json data = nlohmann::json::parse(jsonStr);
        const i32 sceneEntityId = data.at("SceneId");
        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        for (auto b : data.at("Behaviours"))
        {
            const i32 behaviourId = b.at("REI_BEHAVIOUR_ID");
            rei::GetEntityManager().GetBehaviourRegistry().SetBehaviourData(e, behaviourId, b);
        }
    });
}

REI_EXTERN_API inline void AddBehaviour(const i32 sceneEntityId, const i32 behaviourId)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        rei::GetEntityManager().AddBehaviour(e, behaviourId, {});
    });
}

REI_EXTERN_API inline void DeleteBehaviour(const i32 sceneEntityId, const i32 behaviourId)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        rei::GetEntityManager().DeleteBehaviour(e, behaviourId);
    });
}

REI_EXTERN_API inline void SelectEntity(const i32 sceneEntityId)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        GET(e, rei::editor::SelectedTag);
        GET(e, rei::render::RenderOutlineTag);
    });
}

REI_EXTERN_API inline void ResetEntitySelection()
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& selectedEntities = FILTER(rei::editor::SelectedTag);
        FOR(e, selectedEntities)
        {
            DEL(e, rei::editor::SelectedTag);
            DEL(e, rei::render::RenderOutlineTag);
        }
    });
}
