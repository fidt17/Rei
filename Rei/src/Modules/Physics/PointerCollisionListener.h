#pragma once
#include "Collider.h"

namespace rei::physics
{
    struct PointerCollisionListener
    {
        bool DidEnter;
        bool IsInside;
        bool DidExit;
        
        std::shared_ptr<Collider> Collider;
    };
}
EXPORT_COMPONENT(rei::physics::PointerCollisionListener)