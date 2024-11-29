#include "pch.h"
#include "MeshRenderer.h"

namespace rei::render
{
    void MeshRenderer::Render() const
    {
        _mesh.Render(_material->GetShader());
    }

    void MeshRenderer::SetMesh(const Mesh& mesh)
    {
        _mesh = mesh;
    }

    void MeshRenderer::SetMaterial(std::shared_ptr<Material> material)
    {
        _material = material;
    }

    const Material& MeshRenderer::GetMaterial() const
    {
        return *_material;
    }

    const Shader& MeshRenderer::GetShader() const
    {
        return _material->GetShader();
    }
}
