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
        SERIALIZE assets::AssetRef<Material> _material = assets::AssetRef<Material>("rei_simple_lit.mat");

    public:
        REI_API void AfterREI_SET() override;

        REI_API void Init() override;

        void Render() const;

        REI_API void SetModel(const assets::AssetRef<Model>& model);
        REI_API void SetMaterial(const assets::AssetRef<Material>& material);

        REI_API assets::AssetRef<Model>& GetModel();
        REI_API assets::AssetRef<Material>& GetMaterial();
        REI_API const Material& GetRenderMaterial() const;

    private:
        void ConfigureSelectionCollider() const;
        std::string _loadedModelId{};
    };
}

EXPORT_COMPONENT(rei::render::MeshRenderer)
