#include "pch.h"

#include "Image.h"

#include "Engine/Engine.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Physics/PointerCollisionListener.h"

namespace rei::ui
{
    void Image::AfterREI_SET()
    {
        EnsureMaterialInstance();
        SyncMaterialProperties();
        ConfigurePointerInteraction();
    }

    void Image::Init()
    {
        EnsureMaterialInstance();
        SyncMaterialProperties();
        ConfigurePointerInteraction();
    }

    void Image::Dispose()
    {
    }

    const assets::AssetRef<render::Texture>& Image::GetTexture() const
    {
        return _texture;
    }

    render::Color Image::GetColor() const
    {
        return _color;
    }

    void Image::SetColor(const render::Color& color)
    {
        _color = color;
        SyncMaterialProperties();
    }

    bool Image::PreserveAspect() const
    {
        return _preserveAspect;
    }

    bool Image::IsRaycastTarget() const
    {
        return _raycastTarget;
    }

    const render::Material& Image::GetRenderMaterial() const
    {
        if (_materialInstance.IsLoaded()) return *_materialInstance.Get();

        static assets::AssetRef<render::Material> fallbackMaterial = GetAssetManager().GetById<render::Material>(REI_ERROR_MATERIAL_ID);
        return *fallbackMaterial.Get();
    }

    void Image::EnsureMaterialInstance()
    {
        if (_materialInstance.IsLoaded()) return;

        const auto baseMaterial = GetAssetManager().GetById<render::Material>(REI_IMAGE_MATERIAL_ID);
        if (!baseMaterial.IsLoaded())
        {
            LOG_ERROR("Failed to load image material '{}'.", REI_IMAGE_MATERIAL_ID)
            return;
        }

        _materialInstance = render::Material::CreateInstanceFrom(*baseMaterial.Get());
    }

    void Image::SyncMaterialProperties()
    {
        if (!_materialInstance.IsLoaded()) return;

        _materialInstance->SetColor("_Color", _color);
        _materialInstance->SetTexture("_MainTex", _texture);
    }

    void Image::ConfigurePointerInteraction() const
    {
        ECS_WORLD(GetInternalWorld())

        const auto entity = GetEntity();
        GET(entity, physics::PointerCollisionListener);
        GET(entity, editor::SelectableByPointerTag);
    }
}
