#include "pch.h"
#include "EntityRotateAction.h"

namespace rei::editor
{
    EntityRotateAction::EntityRotateAction(ecs::ComponentRef<Transform> transform, glm::quat before, glm::quat after) :
        _transform(transform), _before(before), _after(after)
    {
    }

    bool EntityRotateAction::Undo() { return Apply(_before); }
    bool EntityRotateAction::Redo() { return Apply(_after); }

    bool EntityRotateAction::Apply(const glm::quat& rotation)
    {
        if (_transform.IsNull()) return false;
        _transform.Get().SetRotation(rotation);
        return true;
    }
}
