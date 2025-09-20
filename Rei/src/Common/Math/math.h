#pragma once
#include "Plane.h"
#include "Ray.h"
#include "Vector3.h"
#include "Modules/Render/Mesh/Face.h"
#include "glm/fwd.hpp"

#define PI 3.14159265358979323846

namespace rei::math
{
    REI_API glm::mat4 GetRotationMatrix(const glm::quat& rotation);
    REI_API glm::mat4 GetTransformationMatrix(const Vector3& position, const glm::quat& rotation, const Vector3& scale);

    REI_API glm::quat LookAt(Vector3 direction, Vector3 up);
    REI_API Vector3 GetEulerAngles(const glm::quat& rotation);
    REI_API glm::quat GetQuaternion(const Vector3& eulerAngles);
    REI_API glm::quat GetQuaternion(const glm::mat4& rotationMatrix);

    REI_API bool SphereRayIntersection(const Vector3& center, f32 radius, const Ray& ray, Vector3& out_intersectionPoint);
    REI_API bool BoxRayIntersection(const Vector3& boxSize, const Ray& ray, const glm::mat4& modelMatrix);
    REI_API bool FaceRayIntersection(const render::Face& face, const Ray& ray, const glm::mat4& modelMatrix, Vector3& out_intersectionPoint);
    REI_API bool PlaneRayIntersection(const Plane& plane, const Ray& ray, Vector3& out_intersectionPoint);
}
