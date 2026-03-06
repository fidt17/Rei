#pragma once
#include "Collider.h"

namespace rei::physics
{
    struct PointerCollisionListener
    {
        bool DidEnter;
        bool IsInside;
        bool DidExit;
        math::Vector3 CollisionPoint;
        
        std::shared_ptr<Collider> Collider;
    };
}
EXPORT_COMPONENT(rei::physics::PointerCollisionListener)