#include "pch.h"
#include "MeshRenderer.h"

namespace rei::render
{
    void MeshRenderer::Render() const
    {
        _mesh.Render(_shader);
    }

    void MeshRenderer::SetMesh(const Mesh& mesh)
    {
        _mesh = mesh;
    }

    void MeshRenderer::SetShader(const Shader& shader)
    {
        _shader = shader;
    }
}
