#pragma once
#include "Common/Primitives.h"
#include "glm/glm.hpp"
#include "glm/gtc/quaternion.hpp"
#include "glm/gtc/constants.hpp"
#include <algorithm>
#include <cmath>
#include <vector>

namespace rei::render::light_gizmo_geometry
{
    inline constexpr i32 SEGMENTS = 64;

    struct Shape
    {
        std::vector<glm::vec3> Outer;
        std::vector<glm::vec3> Inner;
    };

    inline void Line(std::vector<glm::vec3>& vertices, const glm::vec3& start, const glm::vec3& end)
    {
        vertices.push_back(start);
        vertices.push_back(end);
    }

    inline void Circle(std::vector<glm::vec3>& vertices, const glm::vec3& center, const glm::vec3& right, const glm::vec3& up, const f32 radius)
    {
        for (i32 i = 0; i < SEGMENTS; ++i)
        {
            const auto start = glm::two_pi<f32>() * i / SEGMENTS;
            const auto end = glm::two_pi<f32>() * (i + 1) / SEGMENTS;
            Line(vertices, center + radius * (right * std::cos(start) + up * std::sin(start)), center + radius * (right * std::cos(end) + up * std::sin(end)));
        }
    }

    inline Shape Point(const glm::vec3& position, const f32 range)
    {
        Shape result;
        if (range <= 0 || !std::isfinite(range)) return result;
        Circle(result.Outer, position, {1, 0, 0}, {0, 1, 0}, range);
        Circle(result.Outer, position, {0, 1, 0}, {0, 0, 1}, range);
        Circle(result.Outer, position, {0, 0, 1}, {1, 0, 0}, range);
        return result;
    }

    inline void Cone(std::vector<glm::vec3>& vertices, const glm::vec3& position, const glm::quat& rotation, const f32 range, const f32 angle, const bool cap)
    {
        const auto forward = rotation * glm::vec3(0, 0, 1);
        const auto right = rotation * glm::vec3(1, 0, 0);
        const auto up = rotation * glm::vec3(0, 1, 0);
        const auto halfAngle = glm::radians(angle * 0.5f);
        // Range is radial distance, so cone meets a spherical cap rather than a plane at z = range.
        const auto center = position + forward * (range * std::cos(halfAngle));
        const auto radius = range * std::sin(halfAngle);
        Circle(vertices, center, right, up, radius);
        for (const auto& axis : {right, -right, up, -up})
        {
            Line(vertices, position, center + axis * radius);
            if (!cap) continue;
            for (i32 i = 0; i < SEGMENTS / 4; ++i)
            {
                const auto start = halfAngle * i / (SEGMENTS / 4);
                const auto end = halfAngle * (i + 1) / (SEGMENTS / 4);
                Line(vertices, position + range * (forward * std::cos(start) + axis * std::sin(start)), position + range * (forward * std::cos(end) + axis * std::sin(end)));
            }
        }
    }

    inline Shape Spot(const glm::vec3& position, const glm::quat& rotation, const f32 range, const f32 innerAngle, const f32 outerAngle)
    {
        Shape result;
        if (range <= 0 || !std::isfinite(range)) return result;
        Cone(result.Outer, position, rotation, range, outerAngle, true);
        if (innerAngle > 0 && innerAngle < outerAngle) Cone(result.Inner, position, rotation, range, innerAngle, false);
        const auto forward = rotation * glm::vec3(0, 0, 1);
        const auto right = rotation * glm::vec3(1, 0, 0);
        const auto tip = position + forward * range;
        const auto arrow = std::min(range * 0.08f, 0.25f);
        Line(result.Outer, position, tip);
        Line(result.Outer, tip, tip - forward * arrow + right * (arrow * 0.5f));
        Line(result.Outer, tip, tip - forward * arrow - right * (arrow * 0.5f));
        return result;
    }
}
