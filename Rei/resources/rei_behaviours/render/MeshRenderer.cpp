#include "pch.h"
#include "MeshRenderer.h"

#include "Engine/Engine.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Physics/ModelCollider.h"
#include "Modules/Physics/PointerCollisionListener.h"

namespace rei::render
{
    void MeshRenderer::ConfigureSelectionCollider() const
    {
        if (!_model.IsLoaded()) return;

        ECS_WORLD(GetInternalWorld())

        const auto meshCollider = std::make_shared<physics::ModelCollider>();
        meshCollider->SetModel(_model);

        const auto e = GetEntity();
        GET(e, physics::PointerCollisionListener).Collider = meshCollider;
        GET(e, editor::SelectableByPointerTag);
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

        for (const auto& mesh : _model.Asset->GetMeshes())
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
        if (_material.IsLoaded()) return *_material.Asset;

        return *GetAssetManager().GetById<Material>(REI_FALLBACK_MATERIAL_ID).Asset;
    }
}
