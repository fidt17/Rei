#pragma once
#include "Vector3.h"

namespace rei::math
{
    struct Ray
    {
        Ray() = default;

        Ray(const Vector3& origin, const Vector3& direction)
            : Origin(origin),
              Direction(direction)
        {
        }
        
        Vector3 Origin { };
        Vector3 Direction { };

        operator std::string() const;
    };
}
