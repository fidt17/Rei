#include "pch.h"

bool rei::math::SphereRayIntersection(const Vector3& center, const f32 _radius, const Ray& ray)
{
    //https://gamedev.stackexchange.com/questions/96459/fast-ray-sphere-collision-code
    const Vector3 m = ray.Origin - center;
    const f32 b = Vector3::Dot(m, ray.Direction);
    const f32 c = Vector3::Dot(m, m) - _radius * _radius;

    if (c > 0.0f && b > 0.0f) return false;
    const float d = b * b - c;

    return d >= 0;
}
