#pragma once
#include "Face.h"
#include "MeshBVHNode.h"
#include "Vertex.h"

namespace rei::render
{
    class Mesh
    {
    public:
        std::string Name;
        std::vector<Vertex> Vertices;
        std::vector<u32> Indices;
        std::vector<Face> Faces;

        u32 VAO, VBO, EBO;

        MeshBVHNode BVHRoot;

        REI_API Mesh() = default;
        Mesh(resources::BinaryReader& reader);
        Mesh(std::string name, const std::vector<Vertex>& vertices, const std::vector<unsigned>& indices, const std::vector<Face>& faces);

        REI_API void Setup();
        REI_API void Dispose() const;

    private:
        void SetupOpenGlObjects();
        void SetupBVH();
    };
}
