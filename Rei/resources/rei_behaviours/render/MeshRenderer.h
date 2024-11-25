#pragma once
#include "Modules/Render/Mesh/Mesh.h"

namespace rei::render
{
    class MeshRenderer
    {
    public:
        void Render() const;

        void SetMesh(const Mesh& mesh);
        void SetShader(const Shader& shader);

    private:
        Mesh _mesh;
        Shader _shader;
    };
}

EXPORT_COMPONENT(rei::render::MeshRenderer)
