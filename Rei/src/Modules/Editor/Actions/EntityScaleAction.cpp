#include "pch.h"
#include "EntityScaleAction.h"
#include "EditorActionTarget.h"
#include <utility>

namespace rei::editor
{
    EntityScaleAction::EntityScaleAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after) :
        EntityScaleAction(std::vector<Change>{{transform, before, after}})
    {
    }

    EntityScaleAction::EntityScaleAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    EditorActionReplayResult EntityScaleAction::Undo() { return Apply(true); }
    EditorActionReplayResult EntityScaleAction::Redo() { return Apply(false); }

    EditorActionReplayResult EntityScaleAction::Apply(const bool undo)
    {
        if (_changes.empty()) return EditorActionReplayResult::Failed;
        for (auto& change : _changes)
        {
            if (!ResolveEditorActionTarget(change.Target)) return EditorActionReplayResult::TargetMissing;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().GetLocalScale() = undo ? change.Before : change.After;
        }
        return EditorActionReplayResult::Applied;
    }
}
