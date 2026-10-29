#include "pch.h"
#include "RectTransformResizeAction.h"
#include <utility>

namespace rei::editor
{
    RectTransformResizeAction::RectTransformResizeAction(ecs::ComponentRef<ui::RectTransform> target, State before, State after) :
        RectTransformResizeAction(std::vector<Change>{{target, before, after}})
    {
    }

    RectTransformResizeAction::RectTransformResizeAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    bool RectTransformResizeAction::Undo() { return Apply(true); }
    bool RectTransformResizeAction::Redo() { return Apply(false); }

    bool RectTransformResizeAction::Apply(const bool undo)
    {
        if (_changes.empty()) return false;
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return false;
        }
        for (const auto& change : _changes)
        {
            const auto& state = undo ? change.Before : change.After;
            change.Target.Get().GetAnchoredPosition() = state.AnchoredPosition;
            change.Target.Get().GetSizeDelta() = state.SizeDelta;
        }
        return true;
    }
}
