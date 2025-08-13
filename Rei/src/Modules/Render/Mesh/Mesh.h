#pragma once
#include "Vertex.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture.h"

namespace rei::render
{
    class Mesh
    {
    public:
        std::vector<Vertex> Vertices;
        std::vector<u32> Indices;
        u32 VAO, VBO, EBO;

        REI_API Mesh() = default;
        Mesh(resources::BinaryReader& reader);
        Mesh(const std::vector<Vertex>& vertices, const std::vector<unsigned int>& indices);

    private:

        void Setup();
    };
}
