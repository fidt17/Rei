#include "pch.h"
#include "Plane.h"

namespace rei::math
{
    Plane::Plane() = default;

    Plane::Plane(const Vector3& normal, const f32 distance): Normal(Vector3::Normalize(normal)), Distance(distance)
    {
    }

    Plane::Plane(const Vector3& normal, const Vector3& point): Normal(Vector3::Normalize(normal)), Distance(Vector3::Dot(normal, point))
    {
    }
}
