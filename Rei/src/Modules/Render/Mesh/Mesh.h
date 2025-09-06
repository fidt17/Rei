#pragma once
#include "Face.h"
#include "Vertex.h"

namespace rei::render
{
    class Mesh
    {
    public:
        std::vector<Vertex> Vertices;
        std::vector<u32> Indices;
        std::vector<Face> Faces;
        u32 VAO, VBO, EBO;

        REI_API Mesh() = default;
        Mesh(resources::BinaryReader& reader);
        Mesh(const std::vector<Vertex>& vertices, const std::vector<unsigned int>& indices, const std::vector<Face>& faces);

    private:
        void Setup();
    };
}
