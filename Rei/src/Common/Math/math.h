#pragma once
#include "Ray.h"
#include "Vector3.h"
#include "Modules/Render/Mesh/Face.h"
#include "glm/fwd.hpp"

namespace rei::math
{
    template <typename T>
    T lerp(T a, T b, T t)
    {
        return a + t * (b - a);
    }

    bool SphereRayIntersection(const Vector3& center, f32 _radius, const Ray& ray);
    bool BoxRayIntersection(const ::rei::math::Vector3& boxSize, const ::rei::math::Ray& ray, const glm::mat4& modelMatrix);
    bool FaceRayIntersection(const render::Face& face, const Ray& ray, const glm::mat4& modelMatrix);
}
