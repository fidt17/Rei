#include "pch.h"
#include "EntityOrderAction.h"
#include "EditorActionTarget.h"
#include <utility>
#include <algorithm>
#include <unordered_set>
#include "Common/Transform/TransformHierarchyUtility.h"

namespace rei::editor
{
    std::vector<EntityOrderAction::Change> EntityOrderAction::CaptureSiblings(const std::vector<ecs::Entity>& parents)
    {
        std::vector<Change> changes;
        const std::unordered_set<ecs::Entity> uniqueParents(parents.begin(), parents.end());
        for (const auto parent : uniqueParents)
        {
            for (const auto entity : transform_utility::CollectSiblings(parent))
            {
                ecs::ComponentRef<Transform> target(GetInternalWorld()->GetRegistry(), entity);
                const auto order = target.Get().GetChildOrder();
                changes.push_back({target, order, order});
            }
        }
        return changes;
    }

    void EntityOrderAction::CompleteChanges(std::vector<Change>& changes)
    {
        for (auto& change : changes) change.After = change.Target.Get().GetChildOrder();
        std::erase_if(changes, [](const Change& change) { return change.Before == change.After; });
    }

    EntityOrderAction::EntityOrderAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    EditorActionReplayResult EntityOrderAction::Undo() { return Apply(true); }
    EditorActionReplayResult EntityOrderAction::Redo() { return Apply(false); }

    EditorActionReplayResult EntityOrderAction::Apply(const bool undo)
    {
        if (_changes.empty()) return EditorActionReplayResult::Failed;
        for (auto& change : _changes)
        {
            if (!ResolveEditorActionTarget(change.Target)) return EditorActionReplayResult::TargetMissing;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().SetChildOrder(undo ? change.Before : change.After);
        }
        return EditorActionReplayResult::Applied;
    }
}
