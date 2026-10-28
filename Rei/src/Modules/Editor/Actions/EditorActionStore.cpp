#include "pch.h"
#include "EditorActionStore.h"
#include <utility>

namespace rei::editor
{
    u64 EditorActionStore::RecordApplied(std::unique_ptr<IEditorAction> action)
    {
        if (!action) return 0;
        const auto id = _nextId++;
        _actions.emplace(id, std::move(action));
        ActionRecorded(id);
        return id;
    }

    bool EditorActionStore::Undo(const u64 actionId)
    {
        const auto entry = _actions.find(actionId);
        return entry != _actions.end() && entry->second->Undo();
    }

    bool EditorActionStore::Redo(const u64 actionId)
    {
        const auto entry = _actions.find(actionId);
        return entry != _actions.end() && entry->second->Redo();
    }
}
