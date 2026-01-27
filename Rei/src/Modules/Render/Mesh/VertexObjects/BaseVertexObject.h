#pragma once
#include "Modules/Render/Mesh/Mesh.h"

namespace rei::render
{
    class BaseVertexObject
    {
    public:
        virtual ~BaseVertexObject() = default;
        
        Mesh GenerateMesh() const;

    protected:
        virtual std::string GetMeshName() const = 0;
        
        u32 AddVertex(math::Vector3 v);
        u32 AddVertex(f32 x, f32 y, f32 z);
        void AddFace(u32 a, u32 b, u32 c);

    protected:
        std::vector<Vertex> _vertices {};
        std::vector<u32> _indices {};
        std::vector<Face> _faces {};

        u32 _lastVertexIdx = 0;
    };
}
