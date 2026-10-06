#include "pch.h"

#include "glm/gtx/euler_angles.hpp"
#include <array>
#include <limits>
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

    bool AxisAlignedBoxRayIntersection(const Vector3& min, const Vector3& max, const Ray& ray)
    {
        const glm::vec3 origin(ray.Origin);
        const glm::vec3 direction(ray.Direction);
        const glm::vec3 lower(min);
        const glm::vec3 upper(max);
        if (direction.x == 0 && direction.y == 0 && direction.z == 0) return false;

        f32 enter = 0;
        f32 exit = (std::numeric_limits<f32>::infinity)();
        for (i32 axis = 0; axis < 3; ++axis)
        {
            if (!std::isfinite(origin[axis]) || !std::isfinite(direction[axis]) ||
                !std::isfinite(lower[axis]) || !std::isfinite(upper[axis]) || lower[axis] > upper[axis]) return false;
            if (direction[axis] == 0)
            {
                if (origin[axis] < lower[axis] || origin[axis] > upper[axis]) return false;
                continue;
            }

            const f32 first = (lower[axis] - origin[axis]) / direction[axis];
            const f32 second = (upper[axis] - origin[axis]) / direction[axis];
            enter = (std::max)(enter, (std::min)(first, second));
            exit = (std::min)(exit, (std::max)(first, second));
            if (exit < enter) return false;
        }
        return true;
    }

    bool BoxRayIntersection(const Vector3& boxSize, const Ray& ray, const glm::mat4& modelMatrix)
    {
        const auto inverseModel = inverse(modelMatrix);
        const Ray localRay(
            Vector3(glm::vec3(inverseModel * glm::vec4(glm::vec3(ray.Origin), 1))),
            Vector3(glm::vec3(inverseModel * glm::vec4(glm::vec3(ray.Direction), 0))));
        const auto halfSize = boxSize / 2;
        return AxisAlignedBoxRayIntersection(halfSize * -1, halfSize, localRay);
    }

    namespace
    {
        bool IsTriangle(const render::Face& face)
        {
            if (face.Vertices.size() == 3) return true;
#if DEBUG
            LOG_WARNING("Only triangle faces are supported for intersection detection")
#endif
            return false;
        }

        bool IntersectTriangle(const std::array<Vector3, 3>& triangle, const Ray& ray, Vector3& out_intersectionPoint, const f32 determinantScale)
        {
            constexpr f32 EPSILON = 1e-6f;
            const Vector3 edge1 = triangle[1] - triangle[0];
            const Vector3 edge2 = triangle[2] - triangle[0];
            const Vector3 h = Vector3::Cross(ray.Direction, edge2);
            const f32 determinant = Vector3::Dot(edge1, h);
            // Preserve the world-space parallel tolerance for a ray transformed into model space.
            if (!std::isfinite(determinant) || std::fabs(determinant * determinantScale) < EPSILON) return false;

            const f32 inverseDeterminant = 1 / determinant;
            const Vector3 fromVertex = ray.Origin - triangle[0];
            const f32 u = inverseDeterminant * Vector3::Dot(fromVertex, h);
            if (u < 0 || u > 1) return false;

            const Vector3 q = Vector3::Cross(fromVertex, edge1);
            const f32 v = inverseDeterminant * Vector3::Dot(ray.Direction, q);
            if (v < 0 || u + v > 1) return false;

            const f32 t = inverseDeterminant * Vector3::Dot(edge2, q);
            if (!std::isfinite(t) || t < EPSILON) return false;

            out_intersectionPoint = ray.Origin + ray.Direction * t;
            return true;
        }
    }

    bool FaceRayIntersection(const render::Face& face, const Ray& ray, const glm::mat4& modelMatrix, Vector3& out_intersectionPoint)
    {
        if (!IsTriangle(face)) return false;
        const std::array triangle{
            Vector3(glm::vec3(modelMatrix * glm::vec4(face.Vertices[0].Position, 1))),
            Vector3(glm::vec3(modelMatrix * glm::vec4(face.Vertices[1].Position, 1))),
            Vector3(glm::vec3(modelMatrix * glm::vec4(face.Vertices[2].Position, 1)))
        };
        return IntersectTriangle(triangle, ray, out_intersectionPoint, 1);
    }

    bool FaceRayIntersection(const render::Face& face, const Ray& localRay, Vector3& out_intersectionPoint, const f32 determinantScale)
    {
        if (!IsTriangle(face)) return false;
        const std::array triangle{
            Vector3(face.Vertices[0].Position),
            Vector3(face.Vertices[1].Position),
            Vector3(face.Vertices[2].Position)
        };
        return IntersectTriangle(triangle, localRay, out_intersectionPoint, determinantScale);
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
