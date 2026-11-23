#pragma once

#include "IEditorAction.h"
#include "Modules/Scenes/SceneEntity.h"

namespace rei::editor
{
    class EntitySubtreeAction final : public IEditorAction
    {
    public:
        REI_API explicit EntitySubtreeAction(ecs::Entity root);
        REI_API EditorActionReplayResult Undo() override;
        REI_API EditorActionReplayResult Redo() override;

    private:
        std::vector<SceneEntity> _entities;
        i32 _parentId = 0;
        i32 _rootOrder = 0;

        void Capture(ecs::Entity entity);
        EditorActionReplayResult Remove();
        EditorActionReplayResult Restore();
    };
}
