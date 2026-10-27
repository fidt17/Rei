#pragma once
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    // Before and After are local positions relative to the entity parent.
    class EntityMoveAction : public IEditorAction
    {
    public:
        EntityMoveAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after);
        bool Undo() override;
        bool Redo() override;

    private:
        ecs::ComponentRef<Transform> _transform;
        math::Vector3 _before;
        math::Vector3 _after;

        bool Apply(const math::Vector3& position);
    };
}
