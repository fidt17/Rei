#pragma once
#include <string>

#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Editor/EntitySelectionUtility.h"
#include "Modules/EntityManagement/EntityManager.h"
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

REI_EXTERN_API inline void GetSceneEntitiesList(char* outputBuffer, const i32 bufferSize)
{
    std::string response;
    auto task = rei::GetEngine().ExecuteOnMainThread([&response]
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

        response = data.dump();
    });

    task->WaitForCompletion();
    strncpy_s(outputBuffer, bufferSize, response.c_str(), _TRUNCATE);
}

REI_EXTERN_API inline void GetEntityData(const i32 sceneEntityId, char* outputBuffer, const i32 bufferSize)
{
    std::string response;
    auto task = rei::GetEngine().ExecuteOnMainThread([sceneEntityId, &response]
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

        response = data.dump();
    });

    task->WaitForCompletion();
    strncpy_s(outputBuffer, bufferSize, response.c_str(), _TRUNCATE);
}

REI_EXTERN_API inline void RenameEntity(const i32 sceneEntityId, const char* newName)
{
    const std::string newNameStr = newName != nullptr ? newName : "";
    auto task = rei::GetEngine().ExecuteOnMainThread([sceneEntityId, newNameStr]
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        GET(e, EntityInfo).Name = newNameStr;
    });

    task->WaitForCompletion();
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

REI_EXTERN_API inline void InstantiateEntity(const char* json, char* outputBuffer, const i32 bufferSize)
{
    const std::string jsonStr = json;

    auto task = rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        nlohmann::json data = nlohmann::json::parse(jsonStr);
        const i32 sourceEntityId = data.at("SourceEntityId");
        const std::string requestedName = data.value("RequestedName", "");
        const bool includeChildren = data.value("IncludeChildren", true);

        const auto& sourceEntity = rei::GetEntityManager().GetBySceneId(sourceEntityId);
        if (IS_DEAD(sourceEntity)) return;

        const auto clone = rei::GetEntityManager().Instantiate(sourceEntity, requestedName, includeChildren);

        nlohmann::json response;
        response["EntityId"] = GET(clone, EntityInfo).Id;
        strncpy_s(outputBuffer, bufferSize, response.dump().c_str(), _TRUNCATE);
    });

    task->WaitForCompletion();
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

REI_EXTERN_API inline void SelectEntity(const i32 sceneEntityId, const bool resetCurrentSelection = true)
{
    auto task = rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        rei::editor::selection_utility::Select(rei::GetInternalWorld(), e, resetCurrentSelection);
    });

    task->WaitForCompletion();
}

REI_EXTERN_API inline void SetEntitySelection(const char* json)
{
    const std::string jsonStr = json != nullptr ? json : "";
    auto task = rei::GetEngine().ExecuteOnMainThread([jsonStr]
    {
        ECS_WORLD(rei::GetInternalWorld());

        rei::editor::selection_utility::Reset(rei::GetInternalWorld());
        if (jsonStr.empty()) return;

        nlohmann::json data = nlohmann::json::parse(jsonStr);
        if (!data.contains("EntityIds")) return;

        for (const auto& entityIdValue : data.at("EntityIds"))
        {
            const auto sceneEntityId = entityIdValue.get<i32>();
            const auto& entity = rei::GetEntityManager().GetBySceneId(sceneEntityId);
            if (IS_DEAD(entity)) continue;

            rei::editor::selection_utility::Select(rei::GetInternalWorld(), entity, false);
        }
    });

    task->WaitForCompletion();
}

REI_EXTERN_API inline void ResetEntitySelection()
{
    auto task = rei::GetEngine().ExecuteOnMainThread([=]
    {
        rei::editor::selection_utility::Reset(rei::GetInternalWorld());
    });

    task->WaitForCompletion();
}
