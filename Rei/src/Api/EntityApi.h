#pragma once
#include <string>

#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Editor/EntitySelectionUtility.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include "Modules/Editor/Actions/RectTransformChangeAction.h"
#include "Modules/Editor/Actions/EntityScaleAction.h"
#include "Modules/Editor/Actions/EntityRotateAction.h"
#include "Modules/Editor/Actions/EntityOrderAction.h"
#include "Modules/Editor/Actions/EntityReparentAction.h"
#include "Common/Transform/TransformHierarchyUtility.h"
#include "Common/Transform/RectTransformUtility.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/transformation/Transform.h"

REI_EXTERN_API inline void CreateNewEntity(const char* name, char* outputBuffer, const i32 bufferSize)
{
    std::string nameStr = name;
    std::string response;
    auto task = rei::GetEngine().ExecuteOnMainThread([&response, nameStr]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto e = rei::GetEntityManager().CreateNewEntity(nameStr);

        nlohmann::json data;
        data["EntityId"] = GET(e, EntityInfo).Id;
        response = data.dump();
    });

    task->WaitForCompletion();
    strncpy_s(outputBuffer, bufferSize, response.c_str(), _TRUNCATE);
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

REI_EXTERN_API inline void SetEntityParent(const i32 sceneEntityId, const i32 parentSceneEntityId, const i32 order, const i32 recordHierarchyChange, const f32* rectLayout = nullptr)
{
    const bool hasRectLayout = rectLayout != nullptr;
    const rei::math::Vector2 position = hasRectLayout ? rei::math::Vector2(rectLayout[0], rectLayout[1]) : rei::math::Vector2(0, 0);
    const rei::math::Vector2 size = hasRectLayout ? rei::math::Vector2(rectLayout[2], rectLayout[3]) : rei::math::Vector2(0, 0);
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());

        const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
        if (IS_DEAD(e)) return;

        const auto& parent = parentSceneEntityId == 0
            ? rei::ecs::NULL_ENTITY
            : rei::GetEntityManager().GetBySceneId(parentSceneEntityId);

        if (!HAS(e, rei::Transform) || e == parent) return;
        if (parentSceneEntityId != 0 && (IS_DEAD(parent) || !HAS(parent, rei::Transform))) return;

        auto& transform = GET(e, rei::Transform);
        const bool parentChanged = transform.GetParent() != parent;
        const bool isRect = HAS(e, rei::ui::RectTransform);
        const auto canvas = isRect ? rei::ui_utility::FindCanvasEntity(e) : rei::ecs::NULL_ENTITY;
        const auto destinationCanvas = rei::ui_utility::FindCanvasEntity(parent);
        const bool recordRect = hasRectLayout && canvas != rei::ecs::NULL_ENTITY && destinationCanvas != rei::ecs::NULL_ENTITY;
        const bool recordChange = recordHierarchyChange != 0 && rei::GetEngine().IsEditorMode()
            && (!parentChanged || !isRect || recordRect);
        const auto applyParent = [&]
        {
            transform.SetParent(parent, order);
            if (!isRect || !hasRectLayout) return;
            auto& rect = GET(e, rei::ui::RectTransform);
            rect.GetAnchoredPosition() = position;
            rect.GetSizeDelta() = size;
        };
        if (!recordChange)
        {
            applyParent();
            return;
        }

        const rei::editor::EntityReparentAction::State before(transform);
        auto changes = rei::editor::EntityOrderAction::CaptureSiblings({before.Parent, parent});
        applyParent();
        rei::editor::EntityOrderAction::CompleteChanges(changes);
        if (parentChanged)
        {
            rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::EntityReparentAction>(
                rei::ecs::ComponentRef<rei::Transform>(rei::GetInternalWorld()->GetRegistry(), e), before,
                rei::editor::EntityReparentAction::State(transform), std::move(changes)));
        }
        else if (!changes.empty())
        {
            rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::EntityOrderAction>(std::move(changes)));
        }
    });
}

REI_EXTERN_API inline void ReorderEntities(const i32* entityIds, const i32 count, const i32 insertionIndex, const i32 recordHierarchyChange)
{
    if (entityIds == nullptr || count <= 0) return;
    std::vector<i32> ids(entityIds, entityIds + count);
    rei::GetEngine().ExecuteOnMainThread([ids = std::move(ids), insertionIndex, recordHierarchyChange]
    {
        ECS_WORLD(rei::GetInternalWorld());
        std::vector<rei::ecs::Entity> entities;
        for (const auto id : ids)
        {
            const auto entity = rei::GetEntityManager().GetBySceneId(id);
            if (IS_DEAD(entity) || !HAS(entity, rei::Transform)) return;
            entities.push_back(entity);
        }
        std::vector<rei::editor::EntityOrderAction::Change> changes;
        if (recordHierarchyChange != 0 && rei::GetEngine().IsEditorMode())
        {
            changes = rei::editor::EntityOrderAction::CaptureSiblings({GET(entities.front(), rei::Transform).GetParent()});
        }
        if (!rei::transform_utility::ReorderSiblings(entities, insertionIndex)) return;
        rei::editor::EntityOrderAction::CompleteChanges(changes);
        if (!changes.empty()) rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::EntityOrderAction>(std::move(changes)));
    });
}

REI_EXTERN_API inline void ReparentEntities(const i32* entityIds, const i32 count, const i32 parentSceneEntityId, const i32 insertionIndex, const i32 recordHierarchyChange, const f32* rectLayouts = nullptr)
{
    if (entityIds == nullptr || count <= 0) return;
    std::vector<i32> ids(entityIds, entityIds + count);
    std::vector<f32> layouts;
    if (rectLayouts != nullptr) layouts.assign(rectLayouts, rectLayouts + count * 4);
    rei::GetEngine().ExecuteOnMainThread([ids = std::move(ids), layouts = std::move(layouts), parentSceneEntityId, insertionIndex, recordHierarchyChange]
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto parent = parentSceneEntityId == 0 ? rei::ecs::NULL_ENTITY : rei::GetEntityManager().GetBySceneId(parentSceneEntityId);
        if (parentSceneEntityId != 0 && (IS_DEAD(parent) || !HAS(parent, rei::Transform))) return;
        std::vector<rei::ecs::Entity> entities;
        for (const auto id : ids)
        {
            const auto entity = rei::GetEntityManager().GetBySceneId(id);
            if (IS_DEAD(entity) || !HAS(entity, rei::Transform)) return;
            entities.push_back(entity);
        }
        bool record = recordHierarchyChange != 0 && rei::GetEngine().IsEditorMode();
        if (!layouts.empty())
        {
            const auto canvas = rei::ui_utility::FindCanvasEntity(parent);
            record = record && canvas != rei::ecs::NULL_ENTITY;
            for (const auto entity : entities)
            {
                if (!HAS(entity, rei::ui::RectTransform)) return;
                record = record && rei::ui_utility::FindCanvasEntity(entity) != rei::ecs::NULL_ENTITY;
            }
        }
        std::vector<rei::editor::EntityReparentAction::Change> changes;
        std::vector<rei::editor::EntityOrderAction::Change> orders;
        if (record)
        {
            for (const auto entity : entities)
            {
                rei::ecs::ComponentRef<rei::Transform> target(_ecs, entity);
                const rei::editor::EntityReparentAction::State before(target.Get());
                changes.push_back({target, before, before});
            }
            std::vector<rei::ecs::Entity> parents{parent};
            for (const auto entity : entities) parents.push_back(GET(entity, rei::Transform).GetParent());
            orders = rei::editor::EntityOrderAction::CaptureSiblings(parents);
        }
        if (!rei::transform_utility::ReparentEntities(entities, parent, insertionIndex, !layouts.empty())) return;
        for (i32 i = 0; i < static_cast<i32>(layouts.size() / 4); i++)
        {
            auto& rect = GET(entities[i], rei::ui::RectTransform);
            rect.GetAnchoredPosition() = {layouts[i * 4], layouts[i * 4 + 1]};
            rect.GetSizeDelta() = {layouts[i * 4 + 2], layouts[i * 4 + 3]};
        }
        if (!record) return;
        for (auto& change : changes) change.After = rei::editor::EntityReparentAction::State(change.Target.Get());
        rei::editor::EntityOrderAction::CompleteChanges(orders);
        rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::EntityReparentAction>(std::move(changes), std::move(orders)));
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
            const bool recordTransform = data.value("RecordTransformChange", false) && rei::GetEngine().IsEditorMode()
                && HAS(e, rei::Transform) && !HAS(e, rei::ui::RectTransform)
                && behaviourId == GET(e, rei::Transform).GetBehaviourId();
            const bool anchorsEdit = b.size() == 5 && b.contains("_anchorMin") && b.contains("_anchorMax") && b.contains("_anchoredPosition") && b.contains("_sizeDelta");
            const bool pivotEdit = b.size() == 4 && b.contains("_pivot") && b.contains("_anchoredPosition") && b.contains("_sizeDelta");
            const bool recordRect = data.value("RecordTransformChange", false) && rei::GetEngine().IsEditorMode()
                && HAS(e, rei::ui::RectTransform) && behaviourId == GET(e, rei::ui::RectTransform).GetBehaviourId()
                && (anchorsEdit || pivotEdit || (b.size() == 3 && b.contains("_anchoredPosition") && b.contains("_sizeDelta"))
                    || (b.size() == 2 && (b.contains("_anchoredPosition") || b.contains("_sizeDelta"))));
            using RectAction = rei::editor::RectTransformChangeAction;
            const auto beforeRect = recordRect ? RectAction::State::Capture(GET(e, rei::ui::RectTransform)) : RectAction::State{};
            const auto beforePosition = recordTransform ? GET(e, rei::Transform).GetLocalPosition() : rei::math::Vector3{};
            const auto beforeScale = recordTransform ? GET(e, rei::Transform).GetLocalScale() : rei::math::Vector3{};
            const auto beforeRotation = recordTransform ? GET(e, rei::Transform).GetLocalRotation() : glm::quat(1, 0, 0, 0);
            rei::GetEntityManager().GetBehaviourRegistry().SetBehaviourData(e, behaviourId, b);
            if (recordRect)
            {
                auto fields = RectAction::Fields::Position;
                if (anchorsEdit) fields = RectAction::Fields::Anchors;
                else if (pivotEdit) fields = RectAction::Fields::Pivot;
                else if (b.contains("_sizeDelta")) fields = RectAction::Fields::Rectangle;
                const rei::ecs::ComponentRef<rei::ui::RectTransform> target(_ecs, e);
                auto action = std::make_unique<RectAction>(fields, target, beforeRect, RectAction::State::Capture(target.Get()));
                if (action->HasChanges()) rei::GetEngine().GetEditorActions().RecordApplied(std::move(action));
            }
            if (!recordTransform) continue;
            const rei::ecs::ComponentRef<rei::Transform> transform(_ecs, e);
            const auto afterPosition = transform.Get().GetLocalPosition();
            const auto afterScale = transform.Get().GetLocalScale();
            const auto afterRotation = transform.Get().GetLocalRotation();
            auto& actions = rei::GetEngine().GetEditorActions();
            if (b.contains("_position") && glm::vec3(beforePosition) != glm::vec3(afterPosition))
                actions.RecordApplied(std::make_unique<rei::editor::EntityMoveAction>(transform, beforePosition, afterPosition));
            if (b.contains("_scale") && glm::vec3(beforeScale) != glm::vec3(afterScale))
                actions.RecordApplied(std::make_unique<rei::editor::EntityScaleAction>(transform, beforeScale, afterScale));
            if (b.contains("_rotation") && beforeRotation != afterRotation && beforeRotation != -afterRotation)
                actions.RecordApplied(std::make_unique<rei::editor::EntityRotateAction>(transform, beforeRotation, afterRotation));
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
