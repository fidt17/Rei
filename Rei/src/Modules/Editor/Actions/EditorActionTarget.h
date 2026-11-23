#pragma once

#include "Ecs/ComponentRef.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei::editor
{
    template <typename T>
    bool ResolveEditorActionTarget(ecs::ComponentRef<T>& target)
    {
        // A restored subtree uses the original Scene ID and a new ECS handle.
        if (target.IsNull() && target.SceneEntityId != 0) target.Resolve();
        return !target.IsNull();
    }
}
