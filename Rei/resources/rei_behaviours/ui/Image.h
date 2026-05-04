#pragma once

#include "Modules/Render/Color/Color.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Textures/Texture.h"

namespace rei::ui
{
    class Image : public Behaviour
    {
        REQUIRE_COMPONENT(RectTransform)
        BEHAVIOUR_BODY(Image)

        SERIALIZE assets::AssetRef<render::Texture> _texture;
        SERIALIZE render::Color _color = render::Color::White();
        SERIALIZE bool _preserveAspect = false;
        SERIALIZE bool _raycastTarget = true;

        assets::AssetRef<render::Material> _materialInstance;

    public:
        REI_API void AfterREI_SET() override;
        REI_API void Init() override;
        REI_API void Dispose() override;

        REI_API const assets::AssetRef<render::Texture>& GetTexture() const;
        REI_API render::Color GetColor() const;
        REI_API bool PreserveAspect() const;
        REI_API bool IsRaycastTarget() const;
        REI_API const render::Material& GetRenderMaterial() const;

    private:
        void EnsureMaterialInstance();
        void SyncMaterialProperties();
        void ConfigureEditorSelection() const;
    };
}

EXPORT_COMPONENT(rei::ui::Image)
