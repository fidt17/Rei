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

    glm::mat4 GetTransformationMatrix(const Vector3& position, const Vector3& rotation, const Vector3& scale);
    
    bool SphereRayIntersection(const Vector3& center, f32 radius, const Ray& ray);
    bool BoxRayIntersection(const Vector3& boxSize, const Ray& ray, const glm::mat4& modelMatrix);
    bool FaceRayIntersection(const render::Face& face, const Ray& ray, const glm::mat4& modelMatrix);
}
