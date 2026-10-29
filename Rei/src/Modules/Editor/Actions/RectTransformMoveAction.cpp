#include "pch.h"
#include "RectTransformMoveAction.h"

namespace rei::editor
{
    RectTransformMoveAction::RectTransformMoveAction(ecs::ComponentRef<ui::RectTransform> target, math::Vector2 before, math::Vector2 after) :
        _target(target), _before(before), _after(after)
    {
    }

    bool RectTransformMoveAction::Undo() { return Apply(_before); }
    bool RectTransformMoveAction::Redo() { return Apply(_after); }

    bool RectTransformMoveAction::Apply(const math::Vector2& position)
    {
        if (_target.IsNull()) return false;
        _target.Get().GetAnchoredPosition() = position;
        return true;
    }
}
