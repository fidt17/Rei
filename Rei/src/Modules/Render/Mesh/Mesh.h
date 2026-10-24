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

        u32 VAO, VBO, EBO;

        MeshBVHNode BVHRoot;

        REI_API Mesh() = default;
        Mesh(resources::BinaryReader& reader);
        Mesh(std::string name, const std::vector<Vertex>& vertices, const std::vector<u32>& indices);

        Face GetFace(u32 triangle) const;

        REI_API void PostLoad();
        REI_API void Dispose() const;

        REI_API void Render() const;

    private:
        bool _didSetupBvh = false;
        bool _didSetupOpenGlObjects = false;

        void ValidateGeometry() const;
        void SetupOpenGlObjects();
        void SetupBVH();
    };
}
