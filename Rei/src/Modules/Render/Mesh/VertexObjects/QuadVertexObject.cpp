#include "pch.h"
#include "QuadVertexObject.h"

namespace rei::render
{
    QuadVertexObject::QuadVertexObject(const f32 width, const f32 height)
    {
        const f32 halfWidth = width * 0.5f;
        const f32 halfHeight = height * 0.5f;

        const u32 topRight = AddVertex(halfWidth, halfHeight, 0.0f);
        const u32 bottomRight = AddVertex(halfWidth, -halfHeight, 0.0f);
        const u32 bottomLeft = AddVertex(-halfWidth, -halfHeight, 0.0f);
        const u32 topLeft = AddVertex(-halfWidth, halfHeight, 0.0f);

        _vertices[topRight].Normal = {0.0f, 0.0f, 1.0f};
        _vertices[bottomRight].Normal = {0.0f, 0.0f, 1.0f};
        _vertices[bottomLeft].Normal = {0.0f, 0.0f, 1.0f};
        _vertices[topLeft].Normal = {0.0f, 0.0f, 1.0f};

        _vertices[topRight].TexCoords = {1.0f, 1.0f};
        _vertices[bottomRight].TexCoords = {1.0f, 0.0f};
        _vertices[bottomLeft].TexCoords = {0.0f, 0.0f};
        _vertices[topLeft].TexCoords = {0.0f, 1.0f};

        AddFace(topRight, bottomRight, topLeft);
        AddFace(bottomRight, bottomLeft, topLeft);
    }

    std::string QuadVertexObject::GetMeshName() const
    {
        return "Quad";
    }
}
