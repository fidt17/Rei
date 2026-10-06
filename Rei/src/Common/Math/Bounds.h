#pragma once
#include "Common/Primitives.h"
#include "glm/glm.hpp"
#include <cmath>
#include <algorithm>
#include <limits>

namespace rei::math
{
    class Bounds
    {
    public:
        glm::vec3 Min{};
        glm::vec3 Max{};

        bool IsValid() const { return _initialized && !_invalid; }

        void Include(const glm::vec3& point)
        {
            if (!IsFinite(point)) { _invalid = true; return; }
            if (!_initialized)
            {
                Min = Max = point;
                _initialized = true;
                return;
            }
            Min = glm::min(Min, point);
            Max = glm::max(Max, point);
        }

        Bounds Transform(const glm::mat4& matrix) const
        {
            if (!IsValid()) return {};
            // Engine model matrices are affine; unexpected projection must fall back to unknown.
            if (matrix[0][3] != 0 || matrix[1][3] != 0 || matrix[2][3] != 0 || matrix[3][3] != 1) return {};
            const f32 lower[3] = {Min.x, Min.y, Min.z};
            const f32 upper[3] = {Max.x, Max.y, Max.z};
            f32 center[3], extents[3], magnitude[3];
            for (u32 axis = 0; axis < 3; ++axis)
            {
                center[axis] = lower[axis] * 0.5f + upper[axis] * 0.5f;
                extents[axis] = upper[axis] * 0.5f - lower[axis] * 0.5f;
                magnitude[axis] = (std::max)(std::abs(lower[axis]), std::abs(upper[axis]));
            }
            // Matrix storage is column-major. Scalar math avoids GLM temporary/call cost in Debug.
            const auto* values = &matrix[0].x;
            f32 worldMin[3], worldMax[3];
            for (u32 axis = 0; axis < 3; ++axis)
            {
                f32 worldCenter = values[12 + axis];
                f32 worldExtent = 0;
                f32 absoluteProducts = std::abs(worldCenter);
                for (u32 column = 0; column < 3; ++column)
                {
                    const auto element = values[column * 4 + axis];
                    worldCenter += element * center[column];
                    worldExtent += std::abs(element) * extents[column];
                    absoluteProducts += std::abs(element) * magnitude[column];
                }
                // Absolute products cover cancellation, rotation, negative scale and parent shear.
                const auto padding = (std::max)(1.0f, absoluteProducts) * (8 * std::numeric_limits<f32>::epsilon());
                worldMin[axis] = worldCenter - worldExtent - padding;
                worldMax[axis] = worldCenter + worldExtent + padding;
            }
            Bounds result;
            result.Include({worldMin[0], worldMin[1], worldMin[2]});
            result.Include({worldMax[0], worldMax[1], worldMax[2]});
            return result;
        }

        bool IntersectsSphere(const glm::vec3& center, const f32 radius) const
        {
            if (!std::isfinite(radius) || !IsFinite(center) || !IsValid()) return true;
            if (radius <= 0) return false;
            const f64 dx = center.x < Min.x ? static_cast<f64>(Min.x) - center.x : center.x > Max.x ? static_cast<f64>(center.x) - Max.x : 0;
            const f64 dy = center.y < Min.y ? static_cast<f64>(Min.y) - center.y : center.y > Max.y ? static_cast<f64>(center.y) - Max.y : 0;
            const f64 dz = center.z < Min.z ? static_cast<f64>(Min.z) - center.z : center.z > Max.z ? static_cast<f64>(center.z) - Max.z : 0;
            return dx * dx + dy * dy + dz * dz <= static_cast<f64>(radius) * radius;
        }

    private:
        static bool IsFinite(const glm::vec3& value) { return std::isfinite(value.x) && std::isfinite(value.y) && std::isfinite(value.z); }
        bool _initialized = false;
        bool _invalid = false;
    };
}
