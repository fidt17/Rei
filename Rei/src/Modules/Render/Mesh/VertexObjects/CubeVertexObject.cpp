#include "pch.h"
#include "CubeVertexObject.h"

namespace rei::render
{
    CubeVertexObject::CubeVertexObject(const math::Vector3& center, const math::Vector3& size)
    {
        const f32 halfWidth = size.x * 0.5f;
        const f32 halfHeight = size.y * 0.5f;
        const f32 halfDepth = size.z * 0.5f;

        // Front face vertices
        const u32 v0 = AddVertex(math::Vector3(-halfWidth, -halfHeight, halfDepth) + center); // bottom-left-front
        const u32 v1 = AddVertex(math::Vector3(halfWidth, -halfHeight, halfDepth) + center); // bottom-right-front
        const u32 v2 = AddVertex(math::Vector3(halfWidth, halfHeight, halfDepth) + center); // top-right-front
        const u32 v3 = AddVertex(math::Vector3(-halfWidth, halfHeight, halfDepth) + center); // top-left-front

        // Back face vertices
        const u32 v4 = AddVertex(math::Vector3(-halfWidth, -halfHeight, -halfDepth) + center); // bottom-left-back
        const u32 v5 = AddVertex(math::Vector3(halfWidth, -halfHeight, -halfDepth) + center); // bottom-right-back
        const u32 v6 = AddVertex(math::Vector3(halfWidth, halfHeight, -halfDepth) + center); // top-right-back
        const u32 v7 = AddVertex(math::Vector3(-halfWidth, halfHeight, -halfDepth) + center); // top-left-back

        // Front face
        AddFace(v0, v1, v2);
        AddFace(v0, v2, v3);

        // Back face
        AddFace(v5, v4, v7);
        AddFace(v5, v7, v6);

        // Right face
        AddFace(v1, v5, v6);
        AddFace(v1, v6, v2);

        // Left face
        AddFace(v4, v0, v3);
        AddFace(v4, v3, v7);

        // Top face
        AddFace(v3, v2, v6);
        AddFace(v3, v6, v7);

        // Bottom face
        AddFace(v4, v5, v1);
        AddFace(v4, v1, v0);
    }

    std::string CubeVertexObject::GetMeshName() const
    {
        return "Cube";
    }
}
