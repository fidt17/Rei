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
        REI_API void LoadAssets(assets::AssetManager& assetManager) override;

        REI_API void Init() override;

        void Render() const;

        REI_API void SetModel(const assets::AssetRef<Model>& model);
        REI_API void SetMaterial(const assets::AssetRef<Material>& material);

        REI_API assets::AssetRef<Model>& GetModel();
        REI_API assets::AssetRef<Material>& GetMaterial();
        REI_API const Shader& GetRenderShader() const;

    private:
        void BindTextures() const;
        void RenderMesh(const std::vector<Mesh>::value_type& mesh) const;
    };
}

EXPORT_COMPONENT(rei::render::MeshRenderer)
