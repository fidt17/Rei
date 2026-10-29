#pragma once
#include <vector>
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    class RectTransformMoveAction : public IEditorAction
    {
    public:
        struct Change
        {
            ecs::ComponentRef<ui::RectTransform> Target;
            math::Vector2 Before;
            math::Vector2 After;
        };

        REI_API RectTransformMoveAction(ecs::ComponentRef<ui::RectTransform> target, math::Vector2 before, math::Vector2 after);
        REI_API explicit RectTransformMoveAction(std::vector<Change> changes);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        std::vector<Change> _changes;

        bool Apply(bool undo);
    };
}
