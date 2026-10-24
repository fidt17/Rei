#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Common/Time/Stopwatch.h"
#include "Mesh.h"

#include "glad/glad.h"

namespace
{
    u64 ReadBVHNode(rei::resources::BinaryReader& reader, rei::render::MeshBVHNode& node, std::vector<u8>& seen, const i32 depth = 0)
    {
        REI_THROW_IF(depth > rei::render::MeshBVHNode::MAX_DEPTH, "Packed mesh BVH exceeds maximum depth")
        node.Min = reader.GetByType<rei::math::Vector3>();
        node.Max = reader.GetByType<rei::math::Vector3>();
        for (i32 axis = 0; axis < 3; ++axis)
        {
            REI_THROW_IF(!std::isfinite(node.Min[axis]) || !std::isfinite(node.Max[axis]) || node.Min[axis] > node.Max[axis], "Invalid packed mesh BVH bounds")
        }
        node.Triangles = reader.GetVector<u32>();
        const auto count = node.Triangles.size();
        const auto* triangles = node.Triangles.data();
        auto* visited = seen.data();
        const auto triangleCount = seen.size();
        for (size_t i = 0; i < count; ++i)
        {
            const auto triangle = triangles[i];
            REI_THROW_IF(triangle >= triangleCount, "Packed mesh BVH triangle index out of range")
            REI_THROW_IF(visited[triangle] != 0, "Duplicate packed mesh BVH triangle")
            visited[triangle] = 1;
        }
        u64 covered = count;
        for (auto* child : {&node.Left, &node.Right})
        {
            const auto present = reader.GetU8();
            REI_THROW_IF(present > 1, "Invalid packed mesh BVH child flag")
            if (present == 0) continue;
            REI_THROW_IF(!node.Triangles.empty(), "Packed mesh BVH branch contains triangles")
            *child = std::make_shared<rei::render::MeshBVHNode>();
            covered += ReadBVHNode(reader, **child, seen, depth + 1);
        }
        return covered;
    }
}

rei::render::Mesh::Mesh(resources::BinaryReader& reader)
    : VAO(0), VBO(0), EBO(0)
{
    rei::time::Stopwatch timer;
    timer.Start();
    const auto start = reader.GetPosition();
    Name = reader.GetStr();
    Vertices = reader.GetVector<Vertex>();
    Indices = reader.GetVector<u32>();
    const auto buffersMs = timer.ElapsedMs();

    ValidateGeometry();
    std::vector<u8> seen(Indices.size() / 3);
    REI_THROW_IF(ReadBVHNode(reader, BVHRoot, seen) != seen.size(), "Packed mesh BVH omits triangles")
    _didSetupBvh = true;
    const auto totalMs = timer.ElapsedMs();
    if (rei::profiling::IsLoadTimingEnabled()) rei::profiling::WriteLoadTiming(nlohmann::json({
        {"stage", "mesh.read"}, {"name", Name}, {"bytes", reader.GetPosition() - start},
        {"vertices", Vertices.size()}, {"triangles", Indices.size() / 3},
        {"buffersMs", buffersMs}, {"bvhMs", totalMs - buffersMs}, {"totalMs", totalMs}
    }).dump());
}

rei::render::Mesh::Mesh(std::string name, const std::vector<Vertex>& vertices, const std::vector<u32>& indices)
    : Name(std::move(name)), Vertices(vertices), Indices(indices), VAO(0), VBO(0), EBO(0)
{
    ValidateGeometry();
}

void rei::render::Mesh::ValidateGeometry() const
{
    REI_THROW_IF(Indices.size() % 3 != 0, "Mesh index count must be a multiple of 3")
    const auto* indices = Indices.data();
    const auto count = Indices.size();
    const auto vertexCount = Vertices.size();
    for (size_t i = 0; i < count; ++i)
    {
        REI_THROW_IF(indices[i] >= vertexCount, "Mesh vertex index out of range")
    }
}

rei::render::Face rei::render::Mesh::GetFace(const u32 triangle) const
{
    const auto offset = static_cast<size_t>(triangle) * 3;
    return {{Vertices.at(Indices.at(offset)), Vertices.at(Indices.at(offset + 1)), Vertices.at(Indices.at(offset + 2))}};
}

void rei::render::Mesh::SetupOpenGlObjects()
{
    if (_didSetupOpenGlObjects) return;
    rei::time::Stopwatch timer;
    timer.Start();

    glGenVertexArrays(1, &VAO);
    glGenBuffers(1, &VBO);
    glGenBuffers(1, &EBO);

    glBindVertexArray(VAO);

    glBindBuffer(GL_ARRAY_BUFFER, VBO);
    glBufferData(GL_ARRAY_BUFFER, Vertices.size() * sizeof(Vertex), Vertices.data(), GL_STATIC_DRAW);

    glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, EBO);
    glBufferData(GL_ELEMENT_ARRAY_BUFFER, Indices.size() * sizeof(u32), Indices.data(), GL_STATIC_DRAW);

    // vertex positions
    glEnableVertexAttribArray(0);
    glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, sizeof(Vertex), static_cast<void*>(0));

    // vertex normals
    glEnableVertexAttribArray(1);
    glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, sizeof(Vertex), reinterpret_cast<void*>(offsetof(Vertex, Normal)));

    // vertex texture coords
    glEnableVertexAttribArray(2);
    glVertexAttribPointer(2, 2, GL_FLOAT, GL_FALSE, sizeof(Vertex), reinterpret_cast<void*>(offsetof(Vertex, TexCoords)));

    glBindVertexArray(0);
    _didSetupOpenGlObjects = true;
    const auto totalMs = timer.ElapsedMs();
    if (rei::profiling::IsLoadTimingEnabled()) rei::profiling::WriteLoadTiming(nlohmann::json({
        {"stage", "mesh.gpu"}, {"name", Name}, {"bytes", Vertices.size() * sizeof(Vertex) + Indices.size() * sizeof(u32)},
        {"totalMs", totalMs}
    }).dump());
}

void rei::render::Mesh::SetupBVH()
{
    if (_didSetupBvh) return;
    
    BVHRoot.BuildBVH(*this);
    _didSetupBvh = true;
}

void rei::render::Mesh::PostLoad()
{
    SetupBVH();
    SetupOpenGlObjects();
}

void rei::render::Mesh::Dispose() const
{
    if (!_didSetupOpenGlObjects) return;

    glDeleteVertexArrays(1, &VAO);
    glDeleteBuffers(1, &VBO);
    glDeleteBuffers(1, &EBO);
}

void rei::render::Mesh::Render() const
{
    REI_PROFILE_SCOPE(rei::profiling::markers::MESH_SUBMIT.Id);
    glBindVertexArray(VAO);
    rei::profiling::RecordDraw(Indices.size(), Indices.size() / 3);
    glDrawElements(GL_TRIANGLES, Indices.size(), GL_UNSIGNED_INT, 0);
    glBindVertexArray(0);
}
