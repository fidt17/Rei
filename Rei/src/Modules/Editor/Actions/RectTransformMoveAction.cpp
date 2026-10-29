#include "pch.h"
#include "RectTransformMoveAction.h"
#include <utility>

namespace rei::editor
{
    RectTransformMoveAction::RectTransformMoveAction(ecs::ComponentRef<ui::RectTransform> target, math::Vector2 before, math::Vector2 after) :
        RectTransformMoveAction(std::vector<Change>{{target, before, after}})
    {
    }

    RectTransformMoveAction::RectTransformMoveAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    bool RectTransformMoveAction::Undo() { return Apply(true); }
    bool RectTransformMoveAction::Redo() { return Apply(false); }

    bool RectTransformMoveAction::Apply(const bool undo)
    {
        if (_changes.empty()) return false;
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return false;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().GetAnchoredPosition() = undo ? change.Before : change.After;
        }
        return true;
    }
}
