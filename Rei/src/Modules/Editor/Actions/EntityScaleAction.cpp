#include "pch.h"
#include "EntityScaleAction.h"

namespace rei::editor
{
    EntityScaleAction::EntityScaleAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after) :
        _transform(transform), _before(before), _after(after)
    {
    }

    bool EntityScaleAction::Undo() { return Apply(_before); }
    bool EntityScaleAction::Redo() { return Apply(_after); }

    bool EntityScaleAction::Apply(const math::Vector3& scale)
    {
        if (_transform.IsNull()) return false;
        _transform.Get().GetLocalScale() = scale;
        return true;
    }
}
