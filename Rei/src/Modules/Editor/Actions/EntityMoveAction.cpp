#include "pch.h"
#include "EntityMoveAction.h"
#include <utility>

namespace rei::editor
{
    EntityMoveAction::EntityMoveAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after) :
        EntityMoveAction(std::vector<Change>{{transform, before, after}})
    {
    }

    EntityMoveAction::EntityMoveAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    bool EntityMoveAction::Undo() { return Apply(true); }
    bool EntityMoveAction::Redo() { return Apply(false); }

    bool EntityMoveAction::Apply(const bool undo)
    {
        if (_changes.empty()) return false;
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return false;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().GetLocalPosition() = undo ? change.Before : change.After;
        }
        return true;
    }
}
