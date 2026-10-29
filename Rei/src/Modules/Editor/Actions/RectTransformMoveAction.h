#pragma once
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    class RectTransformMoveAction : public IEditorAction
    {
    public:
        REI_API RectTransformMoveAction(ecs::ComponentRef<ui::RectTransform> target, math::Vector2 before, math::Vector2 after);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        ecs::ComponentRef<ui::RectTransform> _target;
        math::Vector2 _before;
        math::Vector2 _after;

        bool Apply(const math::Vector2& position);
    };
}
