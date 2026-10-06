#include "pch.h"
#include "MeshRenderer.h"

#include "Engine/Engine.h"
#include "Modules/Editor/RendererSelectionColliderUtility.h"

namespace rei::render
{
    void MeshRenderer::ConfigureSelectionCollider() const
    {
        editor::renderer_selection::Configure(GetEntity(), _model);
    }

    void MeshRenderer::AfterREI_SET()
    {
        if (_loadedModelId != _model.Id)
        {
            if (GetEngine().IsEditor())
            {
                ConfigureSelectionCollider();
            }
        }
        
        _loadedModelId = _model.Id;
    }

    void MeshRenderer::Init()
    {
        if (GetEngine().IsEditor())
        {
            ConfigureSelectionCollider();
        }
    }

    void MeshRenderer::Render() const
    {
        if (!_model.IsLoaded()) return;

        GetRenderMaterial().Use();

        for (const auto& mesh : _model->GetMeshes())
        {
            mesh.Render();
        }
    }

    void MeshRenderer::SetModel(const assets::AssetRef<Model>& model)
    {
        _model = model;

        if (GetEngine().IsEditor())
        {
            ConfigureSelectionCollider();
        }
    }

    void MeshRenderer::SetMaterial(const assets::AssetRef<Material>& material)
    {
        _material = material;
    }

    assets::AssetRef<Model>& MeshRenderer::GetModel()
    {
        return _model;
    }

    assets::AssetRef<Material>& MeshRenderer::GetMaterial()
    {
        return _material;
    }

    const Material& MeshRenderer::GetRenderMaterial() const
    {
        if (_material.IsLoaded()) return *_material.Get();

        static assets::AssetRef<Material> fallbackMaterial = GetAssetManager().GetById<Material>(REI_ERROR_MATERIAL_ID);
        return *fallbackMaterial.Get();
    }
}
