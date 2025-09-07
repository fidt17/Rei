#include "pch.h"
#include "Mesh.h"

#include "glad/glad.h"

rei::render::Mesh::Mesh(resources::BinaryReader& reader)
    : VAO(0), VBO(0), EBO(0)
{
    Name = reader.GetStr();

    Vertices = std::vector<Vertex>(reader.GetI32());
    for (auto& vertex : Vertices)
    {
        vertex = reader.GetByType<Vertex>();
    }

    Indices = std::vector<u32>(reader.GetI32());
    for (auto& vertex : Indices)
    {
        vertex = reader.GetByType<u32>();
    }

    int totalVertices = 0;
    Faces = std::vector<Face>(reader.GetI32());
    for (auto& face : Faces)
    {
        const auto verticesCount = reader.GetI32();
        for (int i = 0; i < verticesCount; i++)
        {
            face.Vertices.emplace_back(reader.GetByType<Vertex>());
            totalVertices++;
        }
    }
}

rei::render::Mesh::Mesh(std::string name, const std::vector<Vertex>& vertices, const std::vector<unsigned int>& indices, const std::vector<Face>& faces)
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
    glGenVertexArrays(1, &VAO);
    glGenBuffers(1, &VBO);
    glGenBuffers(1, &EBO);

    glBindVertexArray(VAO);

    glBindBuffer(GL_ARRAY_BUFFER, VBO);
    glBufferData(GL_ARRAY_BUFFER, Vertices.size() * sizeof(Vertex), Vertices.data(), GL_STATIC_DRAW);

    glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, EBO);
    glBufferData(GL_ELEMENT_ARRAY_BUFFER, Indices.size() * sizeof(unsigned int), Indices.data(), GL_STATIC_DRAW);

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
}

void rei::render::Mesh::SetupBVH()
{
    BVHRoot.BuildBVH(BVHRoot, Faces);
}

void rei::render::Mesh::Setup()
{
    SetupBVH();
    SetupOpenGlObjects();
}

void rei::render::Mesh::Dispose() const
{
    glDeleteVertexArrays(1, &VAO);
    glDeleteBuffers(1, &VBO);
    glDeleteBuffers(1, &EBO);
}
