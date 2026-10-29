#pragma once
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    class RectTransformPivotAction : public IEditorAction
    {
    public:
        struct State
        {
            math::Vector2 Pivot;
            math::Vector2 AnchoredPosition;
            math::Vector2 SizeDelta;
        };

        REI_API RectTransformPivotAction(ecs::ComponentRef<ui::RectTransform> target, State before, State after);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        ecs::ComponentRef<ui::RectTransform> _target;
        State _before;
        State _after;

        bool Apply(const State& state);
    };
}
