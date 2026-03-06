#include "pch.h"
#include "ArrowVertexObject.h"

rei::render::ArrowVertexObject::ArrowVertexObject(const f32 height, const f32 coneHeight, const f32 coneRadius, const f32 cylinderRadius)
{
    constexpr u32 segments = 4;

    // cylinder walls
    for (u32 i = 0; i < segments; i++)
    {
        const f32 angle = PI * 2.0f * i / segments;

        AddVertex(std::cos(angle) * cylinderRadius, std::sin(angle) * cylinderRadius, 0);
        AddVertex(std::cos(angle) * cylinderRadius, std::sin(angle) * cylinderRadius, height - coneHeight);

        if (i == 0) continue;

        AddFace(_lastVertexIdx - 4, _lastVertexIdx - 2, _lastVertexIdx - 1);
        AddFace(_lastVertexIdx - 4, _lastVertexIdx - 3, _lastVertexIdx - 1);
    }

    AddFace(_lastVertexIdx - 2, _lastVertexIdx - 1, 0);
    AddFace(0, _lastVertexIdx - 1, 1);
    // ---

    // cylinder bottom
    const i32 bottomCenter = AddVertex(0, 0, 0);
    for (i32 i = 0; i < segments * 2; i += 2)
    {
        if (i != segments * 2 - 2)
        {
            AddFace(i, bottomCenter, i + 2);
        }
        else
        {
            AddFace(i, bottomCenter, 0);
        }
    }
    // ---

    // cone bottom
    const auto headCircleStart = _lastVertexIdx + 1;
    for (i32 i = 0; i < segments; i++)
    {
        const f32 angle = PI * 2.0f * i / segments;

        AddVertex(std::cos(angle) * cylinderRadius, std::sin(angle) * cylinderRadius, height - coneHeight);
        AddVertex(std::cos(angle) * coneRadius, std::sin(angle) * coneRadius, height - coneHeight);

        if (i == 0) continue;

        AddFace(_lastVertexIdx - 4, _lastVertexIdx - 2, _lastVertexIdx - 1);
        AddFace(_lastVertexIdx - 4, _lastVertexIdx - 3, _lastVertexIdx - 1);
    }

    AddFace(_lastVertexIdx - 2, _lastVertexIdx - 1, headCircleStart);
    AddFace(headCircleStart, _lastVertexIdx - 2, headCircleStart - 1);
    // ---

    // cone walls
    const i32 topCenter = AddVertex(0, 0, height);
    for (i32 i = 0; i < segments * 2; i += 2)
    {
        auto idx = headCircleStart + i;
        if (i != segments * 2 - 2)
        {
            AddFace(idx, topCenter, idx + 2);
        }
        else
        {
            AddFace(idx, topCenter, headCircleStart);
        }
    }
    // ---
}

std::string rei::render::ArrowVertexObject::GetMeshName() const
{
    return "Arrow";
}
