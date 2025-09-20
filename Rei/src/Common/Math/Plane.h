#pragma once
#include "Vector3.h"

namespace rei::math
{
    struct REI_API Plane
    {
        Vector3 Normal = {0, 1, 0};
        f32 Distance = 0;

        Plane();
        Plane(const Vector3& normal, f32 distance);
        Plane(const Vector3& normal, const Vector3& point);
    };
}
