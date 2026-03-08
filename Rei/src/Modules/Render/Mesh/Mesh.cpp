#include "pch.h"
#include "Mesh.h"

#include "glad/glad.h"

namespace
{
    void ReadFaceVertices(std::vector<rei::render::Face>& faces, rei::resources::BinaryReader& reader)
    {
        for (auto& face : faces)
        {
            const i32 verticesCount = reader.GetI32();
            face.Vertices.reserve(verticesCount);
            for (i32 i = 0; i < verticesCount; i++)
            {
                face.Vertices.emplace_back(reader.GetByType<rei::render::Vertex>());
            }
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
    Name = reader.GetStr();
    Vertices = reader.GetVector<Vertex>();
    Indices = reader.GetVector<u32>();

    Faces = std::vector<Face>(reader.GetI32());
    ReadFaceVertices(Faces, reader);
    
    ReadBVHNode(reader, BVHRoot);
    _didSetupBvh = true;
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
    glBindVertexArray(VAO);
    glDrawElements(GL_TRIANGLES, Indices.size(), GL_UNSIGNED_INT, 0);
    glBindVertexArray(0);
}
