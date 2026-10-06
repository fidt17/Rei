#pragma once
#include "Modules/Physics/SphereCollider.h"

namespace rei::tests
{
    class CountingPointerCollider final : public physics::Collider
    {
    public:
        mutable i32 Calls = 0;
        mutable math::Ray LastRay;
        mutable math::Vector3 CameraPositionAtHit;
        std::function<math::Vector3()> ReadCameraPosition;
        std::shared_ptr<physics::Collider> GeometryOverride;
        physics::SphereCollider Geometry;

        bool IsAvailable() const override { return !GeometryOverride || GeometryOverride->IsAvailable(); }

        physics::ColliderType GetType() const override { return Geometry.GetType(); }

        bool Intersect(const math::Ray& ray, const glm::mat4& matrix, math::Vector3& point) const override
        {
            ++Calls;
            LastRay = ray;
            if (ReadCameraPosition) CameraPositionAtHit = ReadCameraPosition();
            return GeometryOverride ? GeometryOverride->Intersect(ray, matrix, point) : Geometry.Intersect(ray, matrix, point);
        }
    };
}
