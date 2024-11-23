#pragma once
#include "Vertex.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture.h"

namespace rei::render
{
    class Mesh
    {
    public:
        std::vector<Vertex> Vertices;
        std::vector<u32> Indices;
        std::vector<Texture> Textures;

        Mesh(const std::vector<Vertex>& vertices, const std::vector<unsigned int>& indices, const std::vector<Texture>& textures);
                
        void Render(const Shader& shader) const;
        
    private:
        u32 VAO, VBO, EBO;

        void Setup();
    };
}
