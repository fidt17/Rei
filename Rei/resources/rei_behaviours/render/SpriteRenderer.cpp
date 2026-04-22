#include "pch.h"
#include "SpriteRenderer.h"

#include "Engine/Engine.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Physics/ModelCollider.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/Mesh/VertexObjects/QuadVertexObject.h"

namespace rei::render
{
    void SpriteRenderer::AfterREI_SET()
    {
        SyncRuntimeState();
    }

    void SpriteRenderer::Init()
    {
        SyncRuntimeState();
    }

    void SpriteRenderer::Dispose()
    {
    }

    void SpriteRenderer::Render() const
    {
        if (!_model.IsLoaded()) return;

        GetRenderMaterial().Use();

        for (const auto& mesh : _model->GetMeshes())
        {
            mesh.Render();
        }
    }

    void SpriteRenderer::SetColor(const Color& color)
    {
        _color = color;
        SyncMaterialProperties();
    }

    void SpriteRenderer::SetSprite(const assets::AssetRef<Texture>& sprite)
    {
        _sprite = sprite;
        SyncRuntimeState();
    }

    Color SpriteRenderer::GetColor() const
    {
        return _color;
    }

    assets::AssetRef<Texture>& SpriteRenderer::GetSprite()
    {
        return _sprite;
    }

    const assets::AssetRef<Texture>& SpriteRenderer::GetSprite() const
    {
        return _sprite;
    }

    assets::AssetRef<Model>& SpriteRenderer::GetModel()
    {
        return _model;
    }

    const assets::AssetRef<Model>& SpriteRenderer::GetModel() const
    {
        return _model;
    }

    const Material& SpriteRenderer::GetRenderMaterial() const
    {
        if (_materialInstance.IsLoaded()) return *_materialInstance.Get();

        static assets::AssetRef<Material> fallbackMaterial = GetAssetManager().GetById<Material>(REI_ERROR_MATERIAL_ID);
        return *fallbackMaterial.Get();
    }

    void SpriteRenderer::EnsureMaterialInstance()
    {
        if (_materialInstance.IsLoaded()) return;

        const auto baseMaterial = GetAssetManager().GetById<Material>(REI_SPRITE_MATERIAL_ID);
        if (!baseMaterial.IsLoaded())
        {
            LOG_ERROR("Failed to load sprite material '{}'.", REI_SPRITE_MATERIAL_ID)
            return;
        }

        _materialInstance = Material::CreateInstanceFrom(*baseMaterial.Get());
    }

    void SpriteRenderer::SyncRuntimeState()
    {
        EnsureMaterialInstance();

        f32 nextAspectRatio = 1.0f;
        if (_sprite.IsLoaded() && _sprite->GetHeight() > 0)
        {
            nextAspectRatio = static_cast<f32>(_sprite->GetWidth()) / static_cast<f32>(_sprite->GetHeight());
        }

        const bool didSpriteChange = _loadedSpriteId != _sprite.Id;
        const bool didAspectChange = std::abs(_aspectRatio - nextAspectRatio) > 0.0001f;
        if (!_model.IsLoaded() || didSpriteChange || didAspectChange)
        {
            _aspectRatio = nextAspectRatio;
            RebuildQuadModel();
        }

        SyncMaterialProperties();
        _loadedSpriteId = _sprite.Id;
    }

    void SpriteRenderer::SyncMaterialProperties()
    {
        if (!_materialInstance.IsLoaded()) return;

        _materialInstance->SetColor("_Color", _color);
        _materialInstance->SetTexture("_MainTex", _sprite);
    }

    void SpriteRenderer::RebuildQuadModel()
    {
        if (_model.IsLoaded())
        {
            GetAssetManager().Release(_model);
            _model = {};
        }

        f32 width = 1.0f;
        f32 height = 1.0f;

        if (_aspectRatio > 1.0f)
        {
            width = _aspectRatio;
        }
        else if (_aspectRatio > 0.0f)
        {
            height = 1.0f / _aspectRatio;
        }

        _model = GetAssetManager().CreateAsset<Model>("Sprite Quad", QuadVertexObject(width, height).GenerateMesh());

        if (GetEngine().IsEditor())
        {
            ConfigureSelectionCollider();
        }
    }

    void SpriteRenderer::ConfigureSelectionCollider() const
    {
        if (!_model.IsLoaded()) return;

        ECS_WORLD(GetInternalWorld())

        const auto meshCollider = std::make_shared<physics::ModelCollider>();
        meshCollider->SetModel(_model);

        const auto e = GetEntity();
        GET(e, physics::PointerCollisionListener).Collider = meshCollider;
        GET(e, editor::SelectableByPointerTag);
    }
}
