#pragma once
#include "Modules/Physics/Collider.h"

namespace rei::editor
{
    struct EditorSelectionCollider
    {
        std::shared_ptr<physics::Collider> Collider;
    };
}

EXPORT_COMPONENT(rei::editor::EditorSelectionCollider)
