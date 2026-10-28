#pragma once
#include <vector>
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    class EntityRotateAction : public IEditorAction
    {
    public:
        struct Change
        {
            ecs::ComponentRef<Transform> Target;
            glm::quat Before;
            glm::quat After;
        };

        REI_API EntityRotateAction(ecs::ComponentRef<Transform> transform, glm::quat before, glm::quat after);
        REI_API explicit EntityRotateAction(std::vector<Change> changes);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        std::vector<Change> _changes;

        bool Apply(bool undo);
    };
}
