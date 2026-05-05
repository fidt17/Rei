#pragma once

#include "Modules/Render/Color/Color.h"
#include "Modules/Render/Text/Font.h"

namespace rei::ui
{
    class Text : public Behaviour
    {
        REQUIRE_COMPONENT(RectTransform)
        BEHAVIOUR_BODY(Text)

        SERIALIZE std::string _value = "Text";
        SERIALIZE assets::AssetRef<render::Font> _font = assets::AssetRef<render::Font>("rei_roboto-regular.ttf");
        SERIALIZE render::Color _color = render::Color::White();
        SERIALIZE f32 _size = 48.0f;

    public:
        REI_API void LoadAssets(assets::AssetManager& assetManager) override;

        REI_API const std::string& GetValue() const;
        REI_API void SetValue(const std::string& value);
        REI_API const assets::AssetRef<render::Font>& GetFont() const;
        REI_API void SetFont(const assets::AssetRef<render::Font>& font);
        REI_API render::Color GetColor() const;
        REI_API void SetColor(const render::Color& color);
        REI_API f32 GetSize() const;
        REI_API void SetSize(f32 size);
    };
}

EXPORT_COMPONENT(rei::ui::Text)
