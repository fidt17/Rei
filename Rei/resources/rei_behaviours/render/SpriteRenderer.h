#pragma once
#include "Modules/Render/Color/Color.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Model/Model.h"
#include "Modules/Render/Textures/Texture.h"

namespace rei::render
{
    class SpriteRenderer : public Behaviour
    {
        BEHAVIOUR_BODY(SpriteRenderer)
        
        SERIALIZE Color _color = Color::White();
        SERIALIZE assets::AssetRef<Texture> _sprite;
        
        assets::AssetRef<Material> _materialInstance;
        assets::AssetRef<Model> _model;

        std::string _loadedSpriteId{};
        f32 _aspectRatio = 1.0f;

    public:
        REI_API void AfterREI_SET() override;
        REI_API void Init() override;
        REI_API void Dispose() override;

        void Render() const;

        REI_API void SetColor(const Color& color);
        REI_API void SetSprite(const assets::AssetRef<Texture>& sprite);

        REI_API Color GetColor() const;
        REI_API assets::AssetRef<Texture>& GetSprite();
        REI_API const assets::AssetRef<Texture>& GetSprite() const;
        REI_API assets::AssetRef<Model>& GetModel();
        REI_API const assets::AssetRef<Model>& GetModel() const;
        REI_API const Material& GetRenderMaterial() const;

    private:
        assets::AssetRef<Texture> ResolveRenderTexture() const;
        void EnsureMaterialInstance();
        void SyncRuntimeState();
        void SyncMaterialProperties();
        void RebuildQuadModel();
        void ConfigureSelectionCollider() const;
    };
}

EXPORT_COMPONENT(rei::render::SpriteRenderer)
