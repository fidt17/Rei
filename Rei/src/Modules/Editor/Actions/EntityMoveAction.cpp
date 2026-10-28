#include "pch.h"
#include "EntityMoveAction.h"

namespace rei::editor
{
    EntityMoveAction::EntityMoveAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after) :
        _transform(transform), _before(before), _after(after)
    {
    }

    bool EntityMoveAction::Undo() { return Apply(_before); }
    bool EntityMoveAction::Redo() { return Apply(_after); }

    bool EntityMoveAction::Apply(const math::Vector3& position)
    {
        if (_transform.IsNull()) return false;
        _transform.Get().GetLocalPosition() = position;
        return true;
    }
}
