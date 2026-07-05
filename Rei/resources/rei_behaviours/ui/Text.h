#pragma once

#include "Modules/Render/Color/Color.h"
#include "Modules/Render/UI/Text/Font.h"
#include "Common/Math/Rect.h"

namespace rei::ui
{
    constexpr f32 REI_TEXT_LINE_HEIGHT_MULTIPLIER = 1.2f;

    class Text : public Behaviour
    {
        REQUIRE_COMPONENT(RectTransform)
        BEHAVIOUR_BODY(Text)

        SERIALIZE std::string _value = "Text";
        SERIALIZE assets::AssetRef<render::Font> _font = assets::AssetRef<render::Font>("rei_roboto-regular.ttf");
        SERIALIZE render::Color _color = render::Color::White();
        SERIALIZE f32 _size = 48.0f;
        SERIALIZE bool _autoSize = false;
        SERIALIZE bool _raycastTarget = true;

    public:
        REI_API void AfterREI_SET() override;
        REI_API void Init() override;
        REI_API void LoadAssets(assets::AssetManager& assetManager) override;

        REI_API const std::string& GetValue() const;
        REI_API void SetValue(const std::string& value);
        REI_API const assets::AssetRef<render::Font>& GetFont() const;
        REI_API void SetFont(const assets::AssetRef<render::Font>& font);
        REI_API render::Color GetColor() const;
        REI_API void SetColor(const render::Color& color);
        REI_API f32 GetSize() const;
        REI_API void SetSize(f32 size);
        REI_API bool IsAutoSize() const;
        REI_API void SetAutoSize(bool value);
        REI_API f32 GetRenderSize(const math::Rect& pixelRect) const;
        REI_API f32 GetLineHeight() const;
        REI_API f32 GetLineHeight(f32 size) const;
        REI_API bool IsRaycastTarget() const;
        REI_API math::Rect CalculateRenderRect(const math::Rect& pixelRect) const;

    private:
        math::Rect CalculateRenderRect(const math::Rect& pixelRect, f32 size) const;
        void ConfigurePointerInteraction() const;
    };
}

EXPORT_COMPONENT(rei::ui::Text)
