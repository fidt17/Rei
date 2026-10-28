#pragma once
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    class EntityRotateAction : public IEditorAction
    {
    public:
        REI_API EntityRotateAction(ecs::ComponentRef<Transform> transform, glm::quat before, glm::quat after);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        ecs::ComponentRef<Transform> _transform;
        glm::quat _before;
        glm::quat _after;

        bool Apply(const glm::quat& rotation);
    };
}
