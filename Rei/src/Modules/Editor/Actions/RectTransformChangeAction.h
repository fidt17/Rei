#pragma once
#include <vector>
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    class RectTransformChangeAction : public IEditorAction
    {
    public:
        enum class Fields { Position, Rectangle, Pivot, Anchors };

        struct State
        {
            math::Vector2 AnchoredPosition = {};
            math::Vector2 SizeDelta = {};
            math::Vector2 Pivot = {0.5f, 0.5f};
            math::Vector2 AnchorMin = {0.5f, 0.5f};
            math::Vector2 AnchorMax = {0.5f, 0.5f};

            REI_API static State Capture(const ui::RectTransform& target);
            REI_API bool Matches(const State& other, Fields fields) const;
            REI_API void ApplyTo(ui::RectTransform& target, Fields fields) const;
        };

        struct Change
        {
            ecs::ComponentRef<ui::RectTransform> Target;
            State Before;
            State After;
        };

        REI_API RectTransformChangeAction(Fields fields, ecs::ComponentRef<ui::RectTransform> target, State before, State after);
        REI_API RectTransformChangeAction(Fields fields, std::vector<Change> changes);
        REI_API bool HasChanges() const;
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        Fields _fields;
        std::vector<Change> _changes;

        bool Apply(bool undo);
    };
}
