#pragma once
#include "EntityOrderAction.h"
#include "RectTransformChangeAction.h"
#include <optional>

namespace rei::editor
{
    class EntityReparentAction : public IEditorAction
    {
    public:
        struct State
        {
            ecs::Entity Parent;
            math::Vector3 Position;
            math::Vector3 Scale;
            glm::quat Rotation;
            std::optional<RectTransformChangeAction::State> Rect;

            REI_API explicit State(const Transform& transform);
        };

        struct Change
        {
            ecs::ComponentRef<Transform> Target;
            State Before;
            State After;
        };

        REI_API EntityReparentAction(std::vector<Change> changes, std::vector<EntityOrderAction::Change> orders);
        REI_API EntityReparentAction(ecs::ComponentRef<Transform> target, State before, State after, std::vector<EntityOrderAction::Change> orders);
        REI_API EditorActionReplayResult Undo() override;
        REI_API EditorActionReplayResult Redo() override;

    private:
        std::vector<Change> _changes;
        std::vector<EntityOrderAction::Change> _orders;

        EditorActionReplayResult Apply(bool undo);
    };
}
