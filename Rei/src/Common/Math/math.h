#pragma once
#include "Ray.h"
#include "Vector3.h"

namespace rei::math
{
    template <typename T>
    T lerp(T a, T b, T t)
    {
        return a + t * (b - a);
    }

    bool SphereRayIntersection(const Vector3& center, f32 _radius, const Ray& ray);
}
