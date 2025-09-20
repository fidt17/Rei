#include "pch.h"
#include "SphereCollider.h"

rei::physics::ColliderType rei::physics::SphereCollider::GetType() const
{
    return Sphere;
}

f32 rei::physics::SphereCollider::GetRadius() const
{
    return _radius;
}

void rei::physics::SphereCollider::SetRadius(const f32 radius)
{
    _radius = radius;
}

bool rei::physics::SphereCollider::Intersect(const math::Ray& ray, const glm::mat4& model, math::Vector3& out_intersectionPoint) const
{
    auto pos = math::Vector3(0, 0, 0);
    pos = pos.Transform(model);
    
    return SphereRayIntersection(pos, _radius, ray, out_intersectionPoint);
}
