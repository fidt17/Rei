#include "pch.h"
#include "EntitySubtreeAction.h"
#include "Api/SceneSyncRelay.h"
#include "Common/Transform/TransformHierarchyUtility.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "rei_behaviours/transformation/Transform.h"
#include <map>
#include <utility>
#include <unordered_set>

namespace rei::editor
{
    EntitySubtreeAction::EntitySubtreeAction(const Kind kind, const std::vector<ecs::Entity>& roots) : _kind(kind)
    {
        GetInternalWorld()->Refresh();
        ECS_WORLD(GetInternalWorld())
        for (const auto root : roots)
        {
            const auto& transform = GET(root, Transform);
            const auto parent = transform.GetParent();
            Subtree subtree{IS_DEAD(parent) ? 0 : GET(parent, EntityInfo).Id, transform.GetChildOrder(), {}};
            Capture(root, subtree.Entities);
            _subtrees.push_back(std::move(subtree));
        }
        std::ranges::sort(_subtrees, {}, [](const Subtree& subtree) { return std::pair(subtree.ParentId, subtree.Order); });
    }

    void EntitySubtreeAction::Capture(const ecs::Entity entity, std::vector<SceneEntity>& states)
    {
        ECS_WORLD(GetInternalWorld())
        states.push_back(GetEntityManager().CaptureSceneEntity(entity));
        for (const auto child : GET(entity, Transform).GetChildren()) Capture(child, states);
    }

    EditorActionReplayResult EntitySubtreeAction::Undo()
    {
        return _kind == Kind::Added ? Remove() : Restore();
    }

    EditorActionReplayResult EntitySubtreeAction::Redo()
    {
        return _kind == Kind::Added ? Restore() : Remove();
    }

    EditorActionReplayResult EntitySubtreeAction::Remove()
    {
        ECS_WORLD(GetInternalWorld())
        auto& entities = GetEntityManager();
        std::vector<ecs::Entity> roots;
        std::unordered_set<ecs::Entity> parents;
        for (const auto& subtree : _subtrees)
        {
            const auto root = entities.GetBySceneId(subtree.Entities.front().GetId());
            if (IS_DEAD(root) || !HAS(root, Transform)) return EditorActionReplayResult::TargetMissing;
            roots.push_back(root);
            parents.insert(GET(root, Transform).GetParent());
        }

        for (const auto root : roots) entities.Destroy(root);
        GetInternalWorld()->Refresh();
        for (const auto parent : parents) transform_utility::NormalizeSiblingOrders(parent);
        for (const auto& subtree : _subtrees)
            for (const auto& state : subtree.Entities) GetSceneSyncRelay().RecordHistoryRemoval(state.GetId());
        return EditorActionReplayResult::Applied;
    }

    EditorActionReplayResult EntitySubtreeAction::Restore()
    {
        ECS_WORLD(GetInternalWorld())
        auto& entities = GetEntityManager();
        for (const auto& subtree : _subtrees)
        {
            const auto parent = entities.GetBySceneId(subtree.ParentId);
            if (subtree.ParentId != 0 && (IS_DEAD(parent) || !HAS(parent, Transform))) return EditorActionReplayResult::TargetMissing;
            for (const auto& state : subtree.Entities)
                if (!IS_DEAD(entities.GetBySceneId(state.GetId()))) return EditorActionReplayResult::Failed;
        }

        try
        {
            std::vector<ecs::Entity> restored;
            for (const auto& subtree : _subtrees)
                for (const auto& state : subtree.Entities) restored.push_back(entities.Create(state));
            GetInternalWorld()->Refresh();
            for (const auto entity : restored) GET(entity, Transform).AfterREI_SET();
            entities.ResolveDependencies();

            std::map<i32, std::vector<std::pair<i32, ecs::Entity>>> rootsByParent;
            for (const auto& subtree : _subtrees)
                rootsByParent[subtree.ParentId].emplace_back(subtree.Order, entities.GetBySceneId(subtree.Entities.front().GetId()));

            for (const auto& [parentId, roots] : rootsByParent)
            {
                const auto parent = parentId == 0 ? ecs::NULL_ENTITY : entities.GetBySceneId(parentId);
                auto siblings = transform_utility::CollectSiblings(parent);
                std::erase_if(siblings, [&](const auto entity)
                {
                    return std::ranges::any_of(roots, [&](const auto& root) { return root.second == entity; });
                });
                std::ranges::sort(siblings, {}, [&](const auto entity)
                {
                    return std::pair(GET(entity, Transform).GetChildOrder(), GET(entity, EntityInfo).Id);
                });
                for (const auto& [order, root] : roots)
                    siblings.insert(siblings.begin() + std::clamp(order, 0, static_cast<i32>(siblings.size())), root);
                for (i32 i = 0; i < static_cast<i32>(siblings.size()); i++)
                    GET(siblings[i], Transform).SetChildOrder(i);
            }
        }
        catch (...)
        {
            GetInternalWorld()->Refresh();
            for (const auto& subtree : _subtrees)
            {
                for (const auto& state : subtree.Entities)
                {
                    const auto entity = entities.GetBySceneId(state.GetId());
                    if (!IS_DEAD(entity)) entities.Destroy(entity);
                }
            }
            GetInternalWorld()->Refresh();
            throw;
        }
        return EditorActionReplayResult::Applied;
    }
}
