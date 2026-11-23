#pragma once

#include "Common/Primitives.h"

namespace rei::editor
{
    enum class EditorActionReplayResult : i32
    {
        Failed = 0,
        Applied = 1,
        TargetMissing = 2
    };

    class IEditorAction
    {
    public:
        virtual ~IEditorAction() = default;
        virtual EditorActionReplayResult Undo() = 0;
        virtual EditorActionReplayResult Redo() = 0;
        virtual void OnDiscarded() {}
    };
}
