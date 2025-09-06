#pragma once
#include "Modules/Physics/Collider.h"

namespace rei::editor
{
    struct SelectionCollider
    {
        std::shared_ptr<physics::Collider> Collider;
    };
}
