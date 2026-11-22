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

        REI_API static std::vector<Change> CaptureSiblings(const std::vector<ecs::Entity>& parents);
        REI_API static void CompleteChanges(std::vector<Change>& changes);
        REI_API explicit EntityOrderAction(std::vector<Change> changes);
        REI_API EditorActionReplayResult Undo() override;
        REI_API EditorActionReplayResult Redo() override;

    private:
        std::vector<Change> _changes;

        EditorActionReplayResult Apply(bool undo);
    };
}
