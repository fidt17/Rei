#include "pch.h"
#include "RingVertexObject.h"

namespace rei::render
{
    RingVertexObject::RingVertexObject(const f32 radius, const f32 width, const i32 segments, const f32 thickness)
    {
        const f32 halfWidth = width * 0.5f;
        const f32 outerRadius = radius + halfWidth;
        const f32 innerRadius = (radius - halfWidth) > 0.001f ? (radius - halfWidth) : 0.001f;
        const f32 halfThickness = thickness > 0.001f ? (thickness * 0.5f) : 0.0005f;

        const f32 step = PI * 2.0f / static_cast<f32>(segments);

        for (i32 i = 0; i < segments; ++i)
        {
            const f32 angle0 = step * static_cast<f32>(i);
            const f32 angle1 = step * static_cast<f32>(i + 1);

            const f32 cos0 = std::cos(angle0);
            const f32 sin0 = std::sin(angle0);
            const f32 cos1 = std::cos(angle1);
            const f32 sin1 = std::sin(angle1);

            const math::Vector3 outer0Top(cos0 * outerRadius, sin0 * outerRadius, halfThickness);
            const math::Vector3 outer1Top(cos1 * outerRadius, sin1 * outerRadius, halfThickness);
            const math::Vector3 inner0Top(cos0 * innerRadius, sin0 * innerRadius, halfThickness);
            const math::Vector3 inner1Top(cos1 * innerRadius, sin1 * innerRadius, halfThickness);

            const math::Vector3 outer0Bottom(cos0 * outerRadius, sin0 * outerRadius, -halfThickness);
            const math::Vector3 outer1Bottom(cos1 * outerRadius, sin1 * outerRadius, -halfThickness);
            const math::Vector3 inner0Bottom(cos0 * innerRadius, sin0 * innerRadius, -halfThickness);
            const math::Vector3 inner1Bottom(cos1 * innerRadius, sin1 * innerRadius, -halfThickness);

            const u32 vOuter0Top = AddVertex(outer0Top);
            const u32 vOuter1Top = AddVertex(outer1Top);
            const u32 vInner1Top = AddVertex(inner1Top);
            const u32 vInner0Top = AddVertex(inner0Top);

            const u32 vOuter0Bottom = AddVertex(outer0Bottom);
            const u32 vOuter1Bottom = AddVertex(outer1Bottom);
            const u32 vInner1Bottom = AddVertex(inner1Bottom);
            const u32 vInner0Bottom = AddVertex(inner0Bottom);

            // Top face
            AddFace(vOuter0Top, vOuter1Top, vInner1Top);
            AddFace(vOuter0Top, vInner1Top, vInner0Top);

            // Bottom face
            AddFace(vOuter1Bottom, vOuter0Bottom, vInner0Bottom);
            AddFace(vOuter1Bottom, vInner0Bottom, vInner1Bottom);

            // Outer wall
            AddFace(vOuter0Top, vOuter0Bottom, vOuter1Bottom);
            AddFace(vOuter0Top, vOuter1Bottom, vOuter1Top);

            // Inner wall
            AddFace(vInner1Top, vInner1Bottom, vInner0Bottom);
            AddFace(vInner1Top, vInner0Bottom, vInner0Top);
        }
    }

    std::string RingVertexObject::GetMeshName() const
    {
        return "Ring";
    }
}
