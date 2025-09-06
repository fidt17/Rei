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

bool rei::physics::SphereCollider::Intersect(const math::Ray& ray, const math::Vector3& position, const glm::mat4& model) const
{
    return SphereRayIntersection(position, _radius, ray);
}
