#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Model/Model.h"

namespace rei::render
{
    class MeshRenderer : public Behaviour
    {
        BEHAVIOUR_BODY(MeshRenderer)
    private:
        SERIALIZE assets::AssetRef<Model> _model;
        SERIALIZE assets::AssetRef<Material> _material;

    public:
        void LoadAssets(assets::AssetManager& assetManager) override
        {
            assetManager.Load(_model);
            assetManager.Load(_material);
        }

        void Render() const;

        void SetModel(const assets::AssetRef<Model>& model);
        void SetMaterial(const assets::AssetRef<Material>& material);

        assets::AssetRef<Model>& GetModel();
        assets::AssetRef<Material>& GetMaterial();

    private:
        void BindTextures() const;
        void RenderMesh(const std::vector<Mesh>::value_type& mesh) const;
    };
}

EXPORT_COMPONENT(rei::render::MeshRenderer)
