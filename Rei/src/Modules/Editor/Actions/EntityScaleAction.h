#pragma once
#include "IEditorAction.h"
#include "Ecs/ComponentRef.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    class EntityScaleAction : public IEditorAction
    {
    public:
        REI_API EntityScaleAction(ecs::ComponentRef<Transform> transform, math::Vector3 before, math::Vector3 after);
        REI_API bool Undo() override;
        REI_API bool Redo() override;

    private:
        ecs::ComponentRef<Transform> _transform;
        math::Vector3 _before;
        math::Vector3 _after;

        bool Apply(const math::Vector3& scale);
    };
}
