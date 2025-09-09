#pragma once
#include "Ray.h"
#include "Vector3.h"
#include "Modules/Render/Mesh/Face.h"
#include "glm/fwd.hpp"

namespace rei::math
{
#define PI 3.14159265358979323846
    
    glm::mat4 GetRotationMatrix(const Vector3& rotation);
    glm::mat4 GetTransformationMatrix(const Vector3& position, const Vector3& rotation, const Vector3& scale);

    glm::mat4 LookAt(const Vector3& origin, Vector3 direction, const Vector3& up);
    
    bool SphereRayIntersection(const Vector3& center, f32 radius, const Ray& ray);
    bool BoxRayIntersection(const Vector3& boxSize, const Ray& ray, const glm::mat4& modelMatrix);
    bool FaceRayIntersection(const render::Face& face, const Ray& ray, const glm::mat4& modelMatrix);
}
