#include "pch.h"
#include "EntityRotateAction.h"
#include <utility>

namespace rei::editor
{
    EntityRotateAction::EntityRotateAction(ecs::ComponentRef<Transform> transform, glm::quat before, glm::quat after) :
        EntityRotateAction(std::vector<Change>{{transform, before, after}})
    {
    }

    EntityRotateAction::EntityRotateAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    bool EntityRotateAction::Undo() { return Apply(true); }
    bool EntityRotateAction::Redo() { return Apply(false); }

    bool EntityRotateAction::Apply(const bool undo)
    {
        if (_changes.empty()) return false;
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return false;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().SetRotation(undo ? change.Before : change.After);
        }
        return true;
    }
}
