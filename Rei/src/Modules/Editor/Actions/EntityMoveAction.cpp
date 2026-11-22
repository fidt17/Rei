#include "pch.h"
#include "EntityMoveAction.h"
#include <utility>

namespace rei::editor
{
    EntityMoveAction::EntityMoveAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after) :
        EntityMoveAction(std::vector<Change>{{transform, before, after}})
    {
    }

    EntityMoveAction::EntityMoveAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    EditorActionReplayResult EntityMoveAction::Undo() { return Apply(true); }
    EditorActionReplayResult EntityMoveAction::Redo() { return Apply(false); }

    EditorActionReplayResult EntityMoveAction::Apply(const bool undo)
    {
        if (_changes.empty()) return EditorActionReplayResult::Failed;
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return EditorActionReplayResult::TargetMissing;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().GetLocalPosition() = undo ? change.Before : change.After;
        }
        return EditorActionReplayResult::Applied;
    }
}
