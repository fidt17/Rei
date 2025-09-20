#pragma once
#include "ColliderType.h"
#include "Common/Math/Ray.h"

namespace rei::physics
{
    class Collider
    {
    public:
        Collider() = default;
        virtual ~Collider() = default;

        virtual ColliderType GetType() const = 0;

        virtual bool Intersect(const math::Ray& ray, const glm::mat4& modelMatrix, math::Vector3& out_intersectionPoint) const = 0;
    };
}
