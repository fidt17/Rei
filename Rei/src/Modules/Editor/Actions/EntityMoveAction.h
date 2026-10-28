#pragma once
#include <vector>
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    // Before and After are local positions relative to the entity parent.
    class EntityMoveAction : public IEditorAction
    {
    public:
        struct Change
        {
            ecs::ComponentRef<Transform> Target;
            math::Vector3 Before;
            math::Vector3 After;
        };

        REI_API EntityMoveAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after);
        REI_API explicit EntityMoveAction(std::vector<Change> changes);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        std::vector<Change> _changes;

        bool Apply(bool undo);
    };
}
