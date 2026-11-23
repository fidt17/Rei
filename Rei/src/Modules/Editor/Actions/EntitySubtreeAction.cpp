#include "pch.h"
#include "EntitySubtreeAction.h"
#include "Common/Transform/TransformHierarchyUtility.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    EntitySubtreeAction::EntitySubtreeAction(const ecs::Entity root)
    {
        GetInternalWorld()->Refresh();
        ECS_WORLD(GetInternalWorld())
        const auto& transform = GET(root, Transform);
        const auto parent = transform.GetParent();
        if (!IS_DEAD(parent)) _parentId = GET(parent, EntityInfo).Id;
        _rootOrder = transform.GetChildOrder();
        Capture(root);
    }

    void EntitySubtreeAction::Capture(const ecs::Entity entity)
    {
        ECS_WORLD(GetInternalWorld())
        _entities.push_back(GetEntityManager().CaptureSceneEntity(entity));
        for (const auto child : GET(entity, Transform).GetChildren()) Capture(child);
    }

    EditorActionReplayResult EntitySubtreeAction::Undo() { return Remove(); }
    EditorActionReplayResult EntitySubtreeAction::Redo() { return Restore(); }

    EditorActionReplayResult EntitySubtreeAction::Remove()
    {
        ECS_WORLD(GetInternalWorld())
        auto& entities = GetEntityManager();
        const auto root = entities.GetBySceneId(_entities.front().GetId());
        if (IS_DEAD(root)) return EditorActionReplayResult::TargetMissing;

        const auto parent = GET(root, Transform).GetParent();
        entities.Destroy(root);
        GetInternalWorld()->Refresh();
        transform_utility::NormalizeSiblingOrders(parent);
        return EditorActionReplayResult::Applied;
    }

    EditorActionReplayResult EntitySubtreeAction::Restore()
    {
        ECS_WORLD(GetInternalWorld())
        auto& entities = GetEntityManager();
        const auto parent = _parentId == 0 ? ecs::NULL_ENTITY : entities.GetBySceneId(_parentId);
        if (_parentId != 0 && (IS_DEAD(parent) || !HAS(parent, Transform))) return EditorActionReplayResult::TargetMissing;
        for (const auto& state : _entities)
        {
            if (!IS_DEAD(entities.GetBySceneId(state.GetId()))) return EditorActionReplayResult::Failed;
        }

        try
        {
            std::vector<ecs::Entity> restored;
            for (const auto& state : _entities) restored.push_back(entities.Create(state));
            GetInternalWorld()->Refresh();
            for (const auto entity : restored) GET(entity, Transform).AfterREI_SET();
            entities.ResolveDependencies();
            transform_utility::InsertWithOrder(GET(restored.front(), Transform), parent, _rootOrder);
        }
        catch (...)
        {
            GetInternalWorld()->Refresh();
            for (const auto& state : _entities)
            {
                const auto entity = entities.GetBySceneId(state.GetId());
                if (!IS_DEAD(entity)) entities.Destroy(entity);
            }
            GetInternalWorld()->Refresh();
            throw;
        }
        return EditorActionReplayResult::Applied;
    }
}
