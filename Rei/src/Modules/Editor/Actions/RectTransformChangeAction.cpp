#include "pch.h"
#include "RectTransformChangeAction.h"
#include "EditorActionTarget.h"
#include <algorithm>
#include <utility>

namespace rei::editor
{
    RectTransformChangeAction::State RectTransformChangeAction::State::Capture(const ui::RectTransform& target)
    {
        return {target.GetAnchoredPosition(), target.GetSizeDelta(), target.GetPivot(), target.GetAnchorMin(), target.GetAnchorMax()};
    }

    bool RectTransformChangeAction::State::Matches(const State& other, const Fields fields) const
    {
        if (glm::vec2(AnchoredPosition) != glm::vec2(other.AnchoredPosition)) return false;
        if (fields == Fields::Position) return true;
        if (glm::vec2(SizeDelta) != glm::vec2(other.SizeDelta)) return false;
        if (fields == Fields::Pivot) return glm::vec2(Pivot) == glm::vec2(other.Pivot);
        if (fields == Fields::Anchors) return glm::vec2(AnchorMin) == glm::vec2(other.AnchorMin) && glm::vec2(AnchorMax) == glm::vec2(other.AnchorMax);
        return true;
    }

    void RectTransformChangeAction::State::ApplyTo(ui::RectTransform& target, const Fields fields) const
    {
        target.GetAnchoredPosition() = AnchoredPosition;
        if (fields == Fields::Position) return;
        target.GetSizeDelta() = SizeDelta;
        if (fields == Fields::Pivot) target.GetPivot() = Pivot;
        if (fields == Fields::Anchors)
        {
            target.GetAnchorMin() = AnchorMin;
            target.GetAnchorMax() = AnchorMax;
        }
    }

    RectTransformChangeAction::RectTransformChangeAction(const Fields fields, ecs::ComponentRef<ui::RectTransform> target, State before, State after) :
        RectTransformChangeAction(fields, std::vector<Change>{{target, before, after}})
    {
    }

    RectTransformChangeAction::RectTransformChangeAction(const Fields fields, std::vector<Change> changes) :
        _fields(fields), _changes(std::move(changes))
    {
        std::erase_if(_changes, [fields](const Change& change) { return change.Before.Matches(change.After, fields); });
    }

    bool RectTransformChangeAction::HasChanges() const { return !_changes.empty(); }
    EditorActionReplayResult RectTransformChangeAction::Undo() { return Apply(true); }
    EditorActionReplayResult RectTransformChangeAction::Redo() { return Apply(false); }

    EditorActionReplayResult RectTransformChangeAction::Apply(const bool undo)
    {
        if (!HasChanges()) return EditorActionReplayResult::Failed;
        for (auto& change : _changes)
        {
            if (!ResolveEditorActionTarget(change.Target)) return EditorActionReplayResult::TargetMissing;
        }
        for (const auto& change : _changes)
        {
            (undo ? change.Before : change.After).ApplyTo(change.Target.Get(), _fields);
        }
        return EditorActionReplayResult::Applied;
    }
}
