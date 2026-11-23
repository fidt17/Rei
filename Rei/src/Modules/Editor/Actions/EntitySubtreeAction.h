#pragma once

#include "IEditorAction.h"
#include "Modules/Scenes/SceneEntity.h"

namespace rei::editor
{
    class EntitySubtreeAction final : public IEditorAction
    {
    public:
        enum class Kind { Added, Deleted };

        REI_API EntitySubtreeAction(Kind kind, const std::vector<ecs::Entity>& roots);
        REI_API EditorActionReplayResult Undo() override;
        REI_API EditorActionReplayResult Redo() override;

    private:
        struct Subtree
        {
            i32 ParentId;
            i32 Order;
            std::vector<SceneEntity> Entities;
        };

        Kind _kind;
        std::vector<Subtree> _subtrees;

        static void Capture(ecs::Entity entity, std::vector<SceneEntity>& states);
        EditorActionReplayResult Remove();
        EditorActionReplayResult Restore();
    };
}
