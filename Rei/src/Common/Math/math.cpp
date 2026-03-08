#include "pch.h"

#include "glm/gtx/euler_angles.hpp"
#include <array>
#include <glm/gtc/quaternion.hpp>

namespace rei::math
{
    glm::mat4 GetRotationMatrix(const glm::quat& rotation)
    {
        return mat4_cast(rotation);
    }

    glm::mat4 GetTransformationMatrix(const Vector3& position, const glm::quat& rotation, const Vector3& s)
    {
        auto model = glm::mat4(1.0f);
        model = translate(model, glm::vec3(position));
        model = model * GetRotationMatrix(rotation);
        model = scale(model, glm::vec3(s));

        return model;
    }

    glm::quat LookAt(Vector3 direction, Vector3 up)
    {
        // Normalize inputs
        direction = Vector3::Normalize(direction);
        up = Vector3::Normalize(up);
        Vector3 right;

        // Handle the edge case where direction is parallel to up vector
        if (glm::abs(Vector3::Dot(direction, up)) > 0.9999f)
        {
            // Find a vector that is definitely not parallel to direction
            Vector3 alternativeUp;

            // Check which component of direction has the smallest absolute value
            Vector3 absDir = Vector3::Abs(direction);
            if (absDir.x <= absDir.y && absDir.x <= absDir.z)
            {
                alternativeUp = Vector3(1.0f, 0.0f, 0.0f); // Use x-axis if x is smallest
            }
            else if (absDir.y <= absDir.x && absDir.y <= absDir.z)
            {
                alternativeUp = Vector3(0.0f, 1.0f, 0.0f); // Use y-axis if y is smallest
            }
            else
            {
                alternativeUp = Vector3(0.0f, 0.0f, 1.0f); // Use z-axis if z is smallest
            }

            right = Vector3::Normalize(Vector3::Cross(alternativeUp, direction));
        }
        else
        {
            right = Vector3::Normalize(Vector3::Cross(up, direction));
        }

        const Vector3 newUp = Vector3::Normalize(Vector3::Cross(direction, right));

        return quat_cast(glm::mat3(right, newUp, direction));
    }

    Vector3 GetEulerAngles(const glm::quat& q)
    {
        f32 yaw = 0.0f;
        f32 pitch = 0.0f;
        f32 roll = 0.0f;
        extractEulerAngleYXZ(mat4_cast(q), yaw, pitch, roll);

        const glm::vec3 euler = degrees(glm::vec3(pitch, yaw, roll));
        return Vector3(euler.x, euler.y, euler.z);
    }

    Vector3 GetEulerAngles(const glm::quat& q, const Vector3& referenceEulerAngles)
    {
        auto wrapAngleNear = [](f32 angle, const f32 reference)
        {
            constexpr f32 fullCircleDegrees = 360.0f;
            constexpr f32 halfTurnDegrees = 180.0f;

            while (angle - reference > halfTurnDegrees)
            {
                angle -= fullCircleDegrees;
            }

            while (angle - reference < -halfTurnDegrees)
            {
                angle += fullCircleDegrees;
            }

            return angle;
        };

        auto wrapAnglesNear = [&](const Vector3& eulerAngles)
        {
            return Vector3(
                wrapAngleNear(eulerAngles.x, referenceEulerAngles.x),
                wrapAngleNear(eulerAngles.y, referenceEulerAngles.y),
                wrapAngleNear(eulerAngles.z, referenceEulerAngles.z));
        };

        auto rotationDistanceSquared = [](const Vector3& a, const Vector3& b)
        {
            const Vector3 delta = a - b;
            return delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
        };

        auto isEquivalentRotation = [](const glm::quat& first, const glm::quat& second)
        {
            constexpr f32 quaternionEquivalenceEpsilon = 1e-4f;
            const f32 dot = glm::abs(glm::dot(glm::normalize(first), glm::normalize(second)));
            return (1.0f - dot) <= quaternionEquivalenceEpsilon;
        };

        const Vector3 base = GetEulerAngles(q);
        std::array<Vector3, 4> variants = {
            base,
            Vector3(base.x + 180.0f, 180.0f - base.y, base.z + 180.0f),
            Vector3(base.x + 180.0f, -base.y, base.z + 180.0f),
            Vector3(base.x, -base.y, base.z),
        };

        Vector3 best = wrapAnglesNear(base);
        f32 bestDistance = rotationDistanceSquared(best, referenceEulerAngles);

        for (const auto& variant : variants)
        {
            const Vector3 wrapped = wrapAnglesNear(variant);
            if (!isEquivalentRotation(q, GetQuaternion(wrapped)))
            {
                continue;
            }

            const f32 distance = rotationDistanceSquared(wrapped, referenceEulerAngles);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = wrapped;
            }
        }

        return best;
    }

    glm::quat GetQuaternion(const Vector3& eulerAngles)
    {
        const f32 yaw = glm::radians(eulerAngles.y);
        const f32 pitch = glm::radians(eulerAngles.x);
        const f32 roll = glm::radians(eulerAngles.z);

        const glm::mat4 rotationMatrix = glm::eulerAngleYXZ(yaw, pitch, roll);
        return quat_cast(rotationMatrix);
    }

    glm::quat GetQuaternion(const glm::mat4& rotationMatrix)
    {
        return quat_cast(rotationMatrix);
    }

    bool SphereRayIntersection(const Vector3& center, const f32 radius, const Ray& ray, Vector3& out_intersectionPoint)
    {
        const Vector3 m = ray.Origin - center;
        const f32 b = Vector3::Dot(m, ray.Direction);
        const f32 c = Vector3::Dot(m, m) - radius * radius;

        if (c > 0.0f && b > 0.0f) return false;
        const f32 d = b * b - c;

        if (d < 0.0f) return false;

        // Calculate the intersection distance along the ray
        f32 t = -b - sqrtf(d);

        // If t is negative, ray starts inside sphere, so use the other intersection
        if (t < 0.0f)
        {
            t = -b + sqrtf(d);
        }

        // Calculate the intersection point
        out_intersectionPoint = ray.Origin + ray.Direction * t;

        return true;
    }

    bool BoxRayIntersection(const Vector3& boxSize, const Ray& ray, const glm::mat4& modelMatrix)
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

    bool FaceRayIntersection(const render::Face& face, const Ray& ray, const glm::mat4& modelMatrix, Vector3& out_intersectionPoint)
    {
        constexpr f32 EPSILON = 1e-6f;

        if (face.Vertices.size() != 3)
        {
#if DEBUG
            LOG_WARNING("Only triangle faces are supported for intersection detection")
#endif
            return false;
        }

        std::array triangle{
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

        const f32 f = 1.0f / a;
        const Vector3 s = ray.Origin - Vector3(triangle[0]);
        const f32 u = f * Vector3::Dot(s, h);

        if (u < 0.0f || u > 1.0f)
        {
            return false;
        }

        const Vector3 q = Vector3::Cross(s, edge1);
        const f32 v = f * Vector3::Dot(ray.Direction, q);

        if (v < 0.0f || u + v > 1.0f)
        {
            return false;
        }

        const f32 t = f * Vector3::Dot(edge2, q);
        if (t < EPSILON)
        {
            return false;
        }

        out_intersectionPoint = ray.Origin + ray.Direction * t;
        return true;
    }

    bool PlaneRayIntersection(const Plane& plane, const Ray& ray, Vector3& out_intersectionPoint)
    {
        const f32 denominator = Vector3::Dot(plane.Normal, ray.Direction);
        if (std::abs(denominator) <= 1e-6) return false;

        const f32 t = (plane.Distance - Vector3::Dot(plane.Normal, ray.Origin)) / denominator;

        // Check if intersection is in front of the ray
        if (t < 0) return false;

        out_intersectionPoint = ray.Origin + ray.Direction * t;
        return true;
    }
}
