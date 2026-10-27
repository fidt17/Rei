#pragma once

namespace rei::editor
{
    class IEditorAction
    {
    public:
        virtual ~IEditorAction() = default;
        virtual bool Undo() = 0;
        virtual bool Redo() = 0;
    };
}
