#pragma once
#include <string>
#include <unordered_set>

#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Editor/EntitySelectionUtility.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include "Modules/Editor/Actions/RectTransformMoveAction.h"
#include "Modules/Editor/Actions/RectTransformResizeAction.h"
#include "Modules/Editor/Actions/RectTransformPivotAction.h"
#include "Modules/Editor/Actions/RectTransformAnchorsAction.h"
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
        const bool sameCanvas = canvas != rei::ecs::NULL_ENTITY && canvas == rei::ui_utility::FindCanvasEntity(parent);
        const bool recordChange = recordHierarchyChange != 0 && rei::GetEngine().IsEditorMode()
            && (!parentChanged || !isRect || (hasRectLayout && sameCanvas));
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
        auto siblings = rei::transform_utility::CollectSiblings(before.Parent);
        if (parentChanged)
        {
            const auto destination = rei::transform_utility::CollectSiblings(parent);
            siblings.insert(siblings.end(), destination.begin(), destination.end());
        }
        std::vector<rei::editor::EntityOrderAction::Change> changes;
        for (const auto sibling : siblings)
        {
            rei::ecs::ComponentRef<rei::Transform> target(rei::GetInternalWorld()->GetRegistry(), sibling);
            changes.push_back({target, target.Get().GetChildOrder(), 0});
        }
        applyParent();
        for (auto& change : changes) change.After = change.Target.Get().GetChildOrder();
        std::erase_if(changes, [](const auto& change) { return change.Before == change.After; });
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
            for (const auto sibling : rei::transform_utility::CollectSiblings(GET(entities.front(), rei::Transform).GetParent()))
            {
                rei::ecs::ComponentRef<rei::Transform> target(_ecs, sibling);
                changes.push_back({target, target.Get().GetChildOrder(), 0});
            }
        }
        if (!rei::transform_utility::ReorderSiblings(entities, insertionIndex)) return;
        for (auto& change : changes) change.After = change.Target.Get().GetChildOrder();
        std::erase_if(changes, [](const auto& change) { return change.Before == change.After; });
        if (!changes.empty()) rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::EntityOrderAction>(std::move(changes)));
    });
}

REI_EXTERN_API inline void ReparentEntities(const i32* entityIds, const i32 count, const i32 parentSceneEntityId, const i32 insertionIndex, const i32 recordHierarchyChange)
{
    if (entityIds == nullptr || count <= 0) return;
    std::vector<i32> ids(entityIds, entityIds + count);
    rei::GetEngine().ExecuteOnMainThread([ids = std::move(ids), parentSceneEntityId, insertionIndex, recordHierarchyChange]
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
        const bool record = recordHierarchyChange != 0 && rei::GetEngine().IsEditorMode();
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
            std::unordered_set<rei::ecs::Entity> parents{parent};
            for (const auto entity : entities) parents.insert(GET(entity, rei::Transform).GetParent());
            for (const auto affectedParent : parents)
            {
                for (const auto sibling : rei::transform_utility::CollectSiblings(affectedParent))
                {
                    rei::ecs::ComponentRef<rei::Transform> target(_ecs, sibling);
                    orders.push_back({target, target.Get().GetChildOrder(), 0});
                }
            }
        }
        if (!rei::transform_utility::ReparentEntities(entities, parent, insertionIndex)) return;
        if (!record) return;
        for (auto& change : changes) change.After = rei::editor::EntityReparentAction::State(change.Target.Get());
        for (auto& order : orders) order.After = order.Target.Get().GetChildOrder();
        std::erase_if(orders, [](const auto& order) { return order.Before == order.After; });
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
                    || (b.size() == 2 && (b.contains("_anchoredPosition") || b.contains("_sizeDelta"))
                        && glm::vec2(GET(e, rei::ui::RectTransform).GetAnchorMin()) == glm::vec2(GET(e, rei::ui::RectTransform).GetAnchorMax())));
            const auto beforeAnchorMin = recordRect ? GET(e, rei::ui::RectTransform).GetAnchorMin() : rei::math::Vector2{};
            const auto beforeAnchorMax = recordRect ? GET(e, rei::ui::RectTransform).GetAnchorMax() : rei::math::Vector2{};
            const auto beforeRectPivot = recordRect ? GET(e, rei::ui::RectTransform).GetPivot() : rei::math::Vector2{};
            const auto beforeRectPosition = recordRect ? GET(e, rei::ui::RectTransform).GetAnchoredPosition() : rei::math::Vector2{};
            const auto beforeRectSize = recordRect ? GET(e, rei::ui::RectTransform).GetSizeDelta() : rei::math::Vector2{};
            const auto beforePosition = recordTransform ? GET(e, rei::Transform).GetLocalPosition() : rei::math::Vector3{};
            const auto beforeScale = recordTransform ? GET(e, rei::Transform).GetLocalScale() : rei::math::Vector3{};
            const auto beforeRotation = recordTransform ? GET(e, rei::Transform).GetLocalRotation() : glm::quat(1, 0, 0, 0);
            rei::GetEntityManager().GetBehaviourRegistry().SetBehaviourData(e, behaviourId, b);
            if (recordRect)
            {
                const rei::ecs::ComponentRef<rei::ui::RectTransform> target(_ecs, e);
                const auto after = target.Get().GetAnchoredPosition();
                if (anchorsEdit)
                {
                    const rei::editor::RectTransformAnchorsAction::State before{beforeAnchorMin, beforeAnchorMax, beforeRectPosition, beforeRectSize};
                    const rei::editor::RectTransformAnchorsAction::State changed{target.Get().GetAnchorMin(), target.Get().GetAnchorMax(), after, target.Get().GetSizeDelta()};
                    if (glm::vec2(before.AnchorMin) != glm::vec2(changed.AnchorMin) || glm::vec2(before.AnchorMax) != glm::vec2(changed.AnchorMax)
                        || glm::vec2(before.AnchoredPosition) != glm::vec2(changed.AnchoredPosition) || glm::vec2(before.SizeDelta) != glm::vec2(changed.SizeDelta))
                        rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::RectTransformAnchorsAction>(target, before, changed));
                }
                else if (pivotEdit)
                {
                    const rei::editor::RectTransformPivotAction::State before{beforeRectPivot, beforeRectPosition, beforeRectSize};
                    const rei::editor::RectTransformPivotAction::State changed{target.Get().GetPivot(), after, target.Get().GetSizeDelta()};
                    if (glm::vec2(before.Pivot) != glm::vec2(changed.Pivot) || glm::vec2(before.AnchoredPosition) != glm::vec2(changed.AnchoredPosition) || glm::vec2(before.SizeDelta) != glm::vec2(changed.SizeDelta))
                        rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::RectTransformPivotAction>(target, before, changed));
                }
                else if (b.contains("_sizeDelta"))
                {
                    const rei::editor::RectTransformResizeAction::State before{beforeRectPosition, beforeRectSize};
                    const rei::editor::RectTransformResizeAction::State resized{after, target.Get().GetSizeDelta()};
                    if (glm::vec2(before.AnchoredPosition) != glm::vec2(resized.AnchoredPosition) || glm::vec2(before.SizeDelta) != glm::vec2(resized.SizeDelta))
                        rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::RectTransformResizeAction>(target, before, resized));
                }
                else if (glm::vec2(beforeRectPosition) != glm::vec2(after))
                    rei::GetEngine().GetEditorActions().RecordApplied(std::make_unique<rei::editor::RectTransformMoveAction>(target, beforeRectPosition, after));
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
