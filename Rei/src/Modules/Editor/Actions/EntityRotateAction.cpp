#include "pch.h"
#include "EntityRotateAction.h"
#include "EditorActionTarget.h"
#include <utility>

namespace rei::editor
{
    EntityRotateAction::EntityRotateAction(ecs::ComponentRef<Transform> transform, glm::quat before, glm::quat after) :
        EntityRotateAction(std::vector<Change>{{transform, before, after}})
    {
    }

    EntityRotateAction::EntityRotateAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    EditorActionReplayResult EntityRotateAction::Undo() { return Apply(true); }
    EditorActionReplayResult EntityRotateAction::Redo() { return Apply(false); }

    EditorActionReplayResult EntityRotateAction::Apply(const bool undo)
    {
        if (_changes.empty()) return EditorActionReplayResult::Failed;
        for (auto& change : _changes)
        {
            if (!ResolveEditorActionTarget(change.Target)) return EditorActionReplayResult::TargetMissing;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().SetRotation(undo ? change.Before : change.After);
        }
        return EditorActionReplayResult::Applied;
    }
}
