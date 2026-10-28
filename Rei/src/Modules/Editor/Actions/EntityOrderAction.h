#pragma once
#include <vector>
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    class EntityOrderAction : public IEditorAction
    {
    public:
        struct Change
        {
            ecs::ComponentRef<Transform> Target;
            i32 Before;
            i32 After;
        };

        REI_API explicit EntityOrderAction(std::vector<Change> changes);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        std::vector<Change> _changes;

        bool Apply(bool undo);
    };
}
