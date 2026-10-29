#include "pch.h"
#include "RectTransformAnchorsAction.h"

namespace rei::editor
{
    RectTransformAnchorsAction::RectTransformAnchorsAction(ecs::ComponentRef<ui::RectTransform> target, State before, State after) :
        _target(target), _before(before), _after(after)
    {
    }

    bool RectTransformAnchorsAction::Undo() { return Apply(_before); }
    bool RectTransformAnchorsAction::Redo() { return Apply(_after); }

    bool RectTransformAnchorsAction::Apply(const State& state)
    {
        if (_target.IsNull()) return false;
        auto& target = _target.Get();
        target.GetAnchorMin() = state.AnchorMin;
        target.GetAnchorMax() = state.AnchorMax;
        target.GetAnchoredPosition() = state.AnchoredPosition;
        target.GetSizeDelta() = state.SizeDelta;
        return true;
    }
}
