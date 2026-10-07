#pragma once
#include "Common/Math/Bounds.h"
#include "rei_behaviours/transformation/Transform.h"
#include <algorithm>
#include <cmath>

namespace rei::render::light_utility
{
    inline f32 ClampRange(const f32 value)
    {
        if (!std::isfinite(value) || value <= 0) return 0;
        return std::max(value, 0.01f);
    }

    inline f32 ClampAngle(const f32 value, const f32 fallback)
    {
        return std::isfinite(value) ? std::clamp(value, 0.0f, 179.0f) : fallback;
    }

    inline f32 ConeCosine(const f32 fullAngle) { return std::cos(glm::radians(fullAngle * 0.5f)); }

    // Compose rotations only: parent scale must not distort a light's direction or range.
    inline glm::quat WorldRotation(const Transform& transform)
    {
        auto rotation = transform.GetLocalRotation();
        auto parent = transform.GetParent();
        const auto registry = GetInternalWorld()->GetRegistry();
        while (registry->IsAlive(parent) && registry->Has<Transform>(parent))
        {
            const auto& ancestor = registry->Get<Transform>(parent);
            rotation = ancestor.GetLocalRotation() * rotation;
            parent = ancestor.GetParent();
        }
        return glm::normalize(rotation);
    }

    // Conservative bounding-sphere cone test; actual angular cutoff remains per fragment.
    inline bool IntersectsSpot(const math::Bounds& bounds, const glm::vec3& position, const glm::vec3& direction, const f32 range, const f32 outerCosine)
    {
        if (range <= 0) return false;
        // Roundoff at the spherical cap must not reject a boundary surface.
        if (!bounds.IntersectsSphere(position, range * 1.000001f)) return false;
        if (!bounds.IsValid()) return true;
        const auto center = bounds.Min * 0.5f + bounds.Max * 0.5f;
        const auto radius = glm::length(bounds.Max * 0.5f - bounds.Min * 0.5f);
        const auto offset = center - position;
        const auto distance = glm::length(offset);
        if (!std::isfinite(distance) || !std::isfinite(radius) || distance <= radius) return true;
        const auto halfAngle = std::acos(std::clamp(outerCosine, -1.0f, 1.0f));
        const auto expandedAngle = halfAngle + std::asin(std::clamp(radius / distance, 0.0f, 1.0f));
        return glm::dot(offset / distance, direction) >= std::cos(expandedAngle) - 0.00001f;
    }
}
