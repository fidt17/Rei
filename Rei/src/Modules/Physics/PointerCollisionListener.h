#pragma once
#include "Collider.h"

namespace rei::physics
{
    struct PointerCollisionListener
    {
        bool DidEnter = false;
        bool IsInside = false;
        bool DidExit = false;
        math::Vector3 CollisionPoint = {};
        
        std::shared_ptr<Collider> Collider;
    };
}
EXPORT_COMPONENT(rei::physics::PointerCollisionListener)
