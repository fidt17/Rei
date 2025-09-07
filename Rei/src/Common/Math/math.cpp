#include "pch.h"

#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"

bool rei::math::SphereRayIntersection(const Vector3& center, const f32 _radius, const Ray& ray)
{
    const Vector3 m = ray.Origin - center;
    const f32 b = Vector3::Dot(m, ray.Direction);
    const f32 c = Vector3::Dot(m, m) - _radius * _radius;

    if (c > 0.0f && b > 0.0f) return false;
    const float d = b * b - c;

    return d >= 0;
}

bool rei::math::BoxRayIntersection(const Vector3& boxSize, const Ray& ray, const glm::mat4& modelMatrix)
{
    // Transform ray to box's local space
    glm::mat4 invTransform = inverse(modelMatrix);
    glm::vec3 localOrigin = glm::vec3(invTransform * glm::vec4(glm::vec3(ray.Origin), 1.0f));
    glm::vec3 localDirection = glm::vec3(invTransform * glm::vec4(glm::vec3(ray.Direction), 0.0f));

    const auto halfExtents = glm::vec3(boxSize / 2);
    
    // Now treat as axis-aligned box intersection in local space
    glm::vec3 t1 = (-halfExtents - localOrigin) / localDirection;
    glm::vec3 t2 = (halfExtents - localOrigin) / localDirection;
    
    glm::vec3 tMinVec = min(t1, t2);
    glm::vec3 tMaxVec = max(t1, t2);
    
    const f32 tMin = glm::max(glm::max(tMinVec.x, tMinVec.y), tMinVec.z); // enter point
    const f32 tMax = glm::min(glm::min(tMaxVec.x, tMaxVec.y), tMaxVec.z); // exit point
    
    return tMax >= tMin && tMax >= 0.0f;
}

bool rei::math::FaceRayIntersection(const render::Face& face, const math::Ray& ray, const glm::mat4& modelMatrix)
{
    using math::Vector3;

    constexpr float EPSILON = 1e-6f;

    if (face.Vertices.size() != 3)
    {
#if DEBUG
        LOG_WARNING("Only triangle faces are supported for intersection detection")
#endif
        return false;
    }

    std::array<Vector3, 3> triangle{
        Vector3(glm::vec3(modelMatrix * glm::vec4(face.Vertices[0].Position, 1))),
        Vector3(glm::vec3(modelMatrix * glm::vec4(face.Vertices[1].Position, 1))),
        Vector3(glm::vec3(modelMatrix * glm::vec4(face.Vertices[2].Position, 1))),
    };

    // Möller–Trumbore algorithm
    const Vector3 edge1 = triangle[1] - triangle[0];
    const Vector3 edge2 = triangle[2] - triangle[0];
    const Vector3 h = Vector3::Cross(ray.Direction, edge2);
    const f32 a = Vector3::Dot(edge1, h);

    if (std::fabs(a) < EPSILON)
    {
        return false; // Ray parallel to triangle
    }

    const float f = 1.0f / a;
    const Vector3 s = ray.Origin - Vector3(triangle[0]);
    const float u = f * Vector3::Dot(s, h);

    if (u < 0.0f || u > 1.0f)
    {
        return false;
    }

    const Vector3 q = Vector3::Cross(s, edge1);
    const float v = f * Vector3::Dot(ray.Direction, q);

    if (v < 0.0f || u + v > 1.0f)
    {
        return false;
    }

    const float t = f * Vector3::Dot(edge2, q);
    return t > EPSILON;
}
