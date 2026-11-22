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
        REI_API EditorActionReplayResult Undo() override;
        REI_API EditorActionReplayResult Redo() override;

    private:
        std::vector<Change> _changes;

        EditorActionReplayResult Apply(bool undo);
    };
}
