#include "pch.h"
#include "EntityScaleAction.h"
#include <utility>

namespace rei::editor
{
    EntityScaleAction::EntityScaleAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after) :
        EntityScaleAction(std::vector<Change>{{transform, before, after}})
    {
    }

    EntityScaleAction::EntityScaleAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    bool EntityScaleAction::Undo() { return Apply(true); }
    bool EntityScaleAction::Redo() { return Apply(false); }

    bool EntityScaleAction::Apply(const bool undo)
    {
        if (_changes.empty()) return false;
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return false;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().GetLocalScale() = undo ? change.Before : change.After;
        }
        return true;
    }
}
