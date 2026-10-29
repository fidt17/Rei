#pragma once
#include <vector>
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    class RectTransformResizeAction : public IEditorAction
    {
    public:
        struct State
        {
            math::Vector2 AnchoredPosition;
            math::Vector2 SizeDelta;
        };

        struct Change
        {
            ecs::ComponentRef<ui::RectTransform> Target;
            State Before;
            State After;
        };

        REI_API RectTransformResizeAction(ecs::ComponentRef<ui::RectTransform> target, State before, State after);
        REI_API explicit RectTransformResizeAction(std::vector<Change> changes);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        std::vector<Change> _changes;

        bool Apply(bool undo);
    };
}
