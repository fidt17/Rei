#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Model/Model.h"

namespace rei::render
{
    class MeshRenderer : public Behaviour
    {
        BEHAVIOUR_BODY(MeshRenderer)

    public:
        void RenderMesh(const std::vector<Mesh>::value_type& mesh) const;
        void Render() const;

        void SetModel(const assets::AssetRef<Model>& model);
        void SetMaterial(const assets::AssetRef<Material>& material);

        assets::AssetRef<Model>& GetModel();
        assets::AssetRef<Material>& GetMaterial();

    private:
        assets::AssetRef<Model> _model;
        assets::AssetRef<Material> _material;
        
        void BindTextures() const;
    };
}

EXPORT_COMPONENT(rei::render::MeshRenderer)
