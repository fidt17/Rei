#pragma once
#include "EntityOrderAction.h"

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
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        std::vector<Change> _changes;
        std::vector<EntityOrderAction::Change> _orders;

        bool Apply(bool undo);
    };
}
