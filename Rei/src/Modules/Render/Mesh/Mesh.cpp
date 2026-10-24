#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Common/Time/Stopwatch.h"
#include "Mesh.h"

#include "glad/glad.h"

namespace
{
    void ReadFaceVertices(std::vector<rei::render::Face>& faces, rei::resources::BinaryReader& reader)
    {
        for (auto& face : faces)
        {
            const i32 verticesCount = reader.GetI32();
            REI_THROW_IF(verticesCount != static_cast<i32>(face.Vertices.size()), "Packed mesh face must contain 3 vertices")
            face.Vertices = reader.GetByType<decltype(face.Vertices)>();
        }
    }

    void ReadBVHNode(rei::resources::BinaryReader& reader, rei::render::MeshBVHNode& node)
    {
        node.Min = reader.GetByType<rei::math::Vector3>();
        node.Max = reader.GetByType<rei::math::Vector3>();

        node.Faces = std::vector<rei::render::Face>(reader.GetI32());
        ReadFaceVertices(node.Faces, reader);

        const bool hasLeft = reader.GetU8() != 0;
        if (hasLeft)
        {
            node.Left = std::make_shared<rei::render::MeshBVHNode>();
            ReadBVHNode(reader, *node.Left);
        }
        else
        {
            node.Left = nullptr;
        }

        const bool hasRight = reader.GetU8() != 0;
        if (hasRight)
        {
            node.Right = std::make_shared<rei::render::MeshBVHNode>();
            ReadBVHNode(reader, *node.Right);
        }
        else
        {
            node.Right = nullptr;
        }
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

    Faces = std::vector<Face>(reader.GetI32());
    ReadFaceVertices(Faces, reader);
    const auto facesMs = timer.ElapsedMs();
    ReadBVHNode(reader, BVHRoot);
    _didSetupBvh = true;
    const auto totalMs = timer.ElapsedMs();
    if (rei::profiling::IsLoadTimingEnabled()) rei::profiling::WriteLoadTiming(nlohmann::json({
        {"stage", "mesh.read"}, {"name", Name}, {"bytes", reader.GetPosition() - start},
        {"vertices", Vertices.size()}, {"triangles", Indices.size() / 3}, {"faces", Faces.size()},
        {"buffersMs", buffersMs}, {"facesMs", facesMs - buffersMs}, {"bvhMs", totalMs - facesMs}, {"totalMs", totalMs}
    }).dump());
}

rei::render::Mesh::Mesh(std::string name, const std::vector<Vertex>& vertices, const std::vector<u32>& indices, const std::vector<Face>& faces)
    :
    Name(std::move(name)),
    VAO(0), VBO(0), EBO(0)
{
    Vertices = vertices;
    Indices = indices;
    Faces = faces;
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
    
    BVHRoot.BuildBVH(BVHRoot, Faces);
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
