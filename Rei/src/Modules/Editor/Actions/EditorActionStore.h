#pragma once
#include <memory>
#include <unordered_map>
#include "Common/Primitives.h"
#include "IEditorAction.h"

namespace rei::editor
{
    class EditorActionStore
    {
    public:
        // Stores an already applied edit. Zero means no action was supplied.
        u64 RecordApplied(std::unique_ptr<IEditorAction> action);
        bool Undo(u64 actionId);
        bool Redo(u64 actionId);

    private:
        u64 _nextId = 1;
        std::unordered_map<u64, std::unique_ptr<IEditorAction>> _actions;
    };
}
