#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/Mesh.h"

namespace rei::render
{
    class MeshRenderer : public Behaviour
    {
        BEHAVIOUR_BODY(MeshRenderer)

    public:
        void Render() const;

        void SetMesh(const Mesh& mesh);
        void SetMaterial(std::shared_ptr<Material> material);

        const Material& GetMaterial() const;
        const Shader& GetShader() const;

    private:
        Mesh _mesh;
        std::shared_ptr<Material> _material;
    };
}

EXPORT_COMPONENT(rei::render::MeshRenderer)
