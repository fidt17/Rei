#include "pch.h"
#include "BaseVertexObject.h"

void rei::render::BaseVertexObject::AddFace(const u32 a, const u32 b, const u32 c)
{
    _indices.push_back(a);
    _indices.push_back(b);
    _indices.push_back(c);
}

rei::render::Mesh rei::render::BaseVertexObject::GenerateMesh() const
{
    return Mesh(GetMeshName(), _vertices, _indices);
}

u32 rei::render::BaseVertexObject::AddVertex(math::Vector3 v)
{
    return AddVertex(v.x, v.y, v.z);
}

u32 rei::render::BaseVertexObject::AddVertex(const f32 x, const f32 y, const f32 z)
{
    Vertex v;
    v.Position = glm::vec3(x, y, z);

    _vertices.push_back(v);

    return _lastVertexIdx++;
}
