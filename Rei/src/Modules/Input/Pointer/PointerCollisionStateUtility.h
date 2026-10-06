#pragma once
#include "Modules/Physics/PointerCollisionListener.h"

namespace rei::input
{
    inline void UpdatePointerCollisionState(physics::PointerCollisionListener& listener, const bool isInside)
    {
        listener.DidEnter = isInside && !listener.IsInside;
        listener.DidExit = !isInside && listener.IsInside;
        listener.IsInside = isInside;
    }
}
