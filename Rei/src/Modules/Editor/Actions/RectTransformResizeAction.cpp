#include "pch.h"
#include "RectTransformResizeAction.h"

namespace rei::editor
{
    RectTransformResizeAction::RectTransformResizeAction(ecs::ComponentRef<ui::RectTransform> target, State before, State after) :
        _target(target), _before(before), _after(after)
    {
    }

    bool RectTransformResizeAction::Undo() { return Apply(_before); }
    bool RectTransformResizeAction::Redo() { return Apply(_after); }

    bool RectTransformResizeAction::Apply(const State& state)
    {
        if (_target.IsNull()) return false;
        _target.Get().GetAnchoredPosition() = state.AnchoredPosition;
        _target.Get().GetSizeDelta() = state.SizeDelta;
        return true;
    }
}
