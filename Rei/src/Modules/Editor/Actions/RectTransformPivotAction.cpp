#include "pch.h"
#include "RectTransformPivotAction.h"

namespace rei::editor
{
    RectTransformPivotAction::RectTransformPivotAction(ecs::ComponentRef<ui::RectTransform> target, State before, State after) :
        _target(target), _before(before), _after(after)
    {
    }

    bool RectTransformPivotAction::Undo() { return Apply(_before); }
    bool RectTransformPivotAction::Redo() { return Apply(_after); }

    bool RectTransformPivotAction::Apply(const State& state)
    {
        if (_target.IsNull()) return false;
        auto& target = _target.Get();
        target.GetPivot() = state.Pivot;
        target.GetAnchoredPosition() = state.AnchoredPosition;
        target.GetSizeDelta() = state.SizeDelta;
        return true;
    }
}
