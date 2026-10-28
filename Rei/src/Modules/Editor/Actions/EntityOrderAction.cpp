#include "pch.h"
#include "EntityOrderAction.h"
#include <utility>

namespace rei::editor
{
    EntityOrderAction::EntityOrderAction(std::vector<Change> changes) : _changes(std::move(changes))
    {
    }

    bool EntityOrderAction::Undo() { return Apply(true); }
    bool EntityOrderAction::Redo() { return Apply(false); }

    bool EntityOrderAction::Apply(const bool undo)
    {
        if (_changes.empty()) return false;
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return false;
        }
        for (const auto& change : _changes)
        {
            change.Target.Get().SetChildOrder(undo ? change.Before : change.After);
        }
        return true;
    }
}
