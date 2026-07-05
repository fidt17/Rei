#include "pch.h"

#include "Text.h"

#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Physics/PointerCollisionListener.h"

namespace rei::ui
{
    void Text::AfterREI_SET()
    {
        ConfigurePointerInteraction();
    }

    void Text::Init()
    {
        ConfigurePointerInteraction();
    }

    void Text::LoadAssets(assets::AssetManager& assetManager)
    {
        assetManager.Load(_font);
    }

    const std::string& Text::GetValue() const
    {
        return _value;
    }

    void Text::SetValue(const std::string& value)
    {
        _value = value;
    }

    const assets::AssetRef<render::Font>& Text::GetFont() const
    {
        return _font;
    }

    void Text::SetFont(const assets::AssetRef<render::Font>& font)
    {
        _font = font;
    }

    render::Color Text::GetColor() const
    {
        return _color;
    }

    void Text::SetColor(const render::Color& color)
    {
        _color = color;
    }

    f32 Text::GetSize() const
    {
        return _size;
    }

    void Text::SetSize(const f32 size)
    {
        _size = size;
    }

    bool Text::IsAutoSize() const
    {
        return _autoSize;
    }

    void Text::SetAutoSize(const bool value)
    {
        _autoSize = value;
    }

    f32 Text::GetRenderSize(const math::Rect& pixelRect) const
    {
        if (!_autoSize) return _size;

        const auto targetSize = pixelRect.GetSize();
        if (targetSize.x <= 0.0f || targetSize.y <= 0.0f) return _size;

        const auto textSize = CalculateRenderRect(pixelRect, _size).GetSize();
        if (textSize.x <= 0.0f || textSize.y <= 0.0f) return _size;

        const f32 scale = (std::min)(targetSize.x / textSize.x, targetSize.y / textSize.y);
        if (scale >= 1.0f) return _size;

        return (std::max)(1.0f, _size * scale);
    }

    f32 Text::GetLineHeight() const
    {
        return GetLineHeight(_size);
    }

    f32 Text::GetLineHeight(const f32 size) const
    {
        return size * REI_TEXT_LINE_HEIGHT_MULTIPLIER;
    }

    bool Text::IsRaycastTarget() const
    {
        return _raycastTarget;
    }

    math::Rect Text::CalculateRenderRect(const math::Rect& pixelRect) const
    {
        return CalculateRenderRect(pixelRect, GetRenderSize(pixelRect));
    }

    math::Rect Text::CalculateRenderRect(const math::Rect& pixelRect, const f32 size) const
    {
        if (!_font.IsLoaded()) return {};

        const f32 fontScale = size / static_cast<f32>(_font->GetPixelHeight());
        const f32 lineHeight = GetLineHeight(size);
        const f32 startX = pixelRect.Min.x;
        f32 x = startX;
        f32 y = pixelRect.Max.y - size;

        math::Rect textRect {
            math::Vector2::Max(),
            math::Vector2::Min()
        };

        for (const char character : _value)
        {
            if (character == '\n')
            {
                textRect.Min.x = (std::min)(textRect.Min.x, startX);
                textRect.Max.x = (std::max)(textRect.Max.x, x);
                textRect.Min.y = (std::min)(textRect.Min.y, y);
                textRect.Max.y = (std::max)(textRect.Max.y, y + size);
                x = startX;
                y -= lineHeight;
                continue;
            }

            const auto glyphKey = static_cast<u8>(character);
            if (!_font->HasGlyph(glyphKey)) continue;

            const auto& glyph = _font->GetGlyph(glyphKey);
            const f32 glyphX = x + static_cast<f32>(glyph.BearingX) * fontScale;
            const f32 glyphY = y - static_cast<f32>(glyph.Height - glyph.BearingY) * fontScale;
            const f32 glyphWidth = static_cast<f32>(glyph.Width) * fontScale;
            const f32 glyphHeight = static_cast<f32>(glyph.Height) * fontScale;

            if (glyph.TextureId != 0 && glyphWidth > 0.0f && glyphHeight > 0.0f)
            {
                textRect.Min.x = (std::min)(textRect.Min.x, glyphX);
                textRect.Min.y = (std::min)(textRect.Min.y, glyphY);
                textRect.Max.x = (std::max)(textRect.Max.x, glyphX + glyphWidth);
                textRect.Max.y = (std::max)(textRect.Max.y, glyphY + glyphHeight);
            }

            x += glyph.GetAdvancePixels() * fontScale;
        }

        textRect.Min.x = (std::min)(textRect.Min.x, startX);
        textRect.Max.x = (std::max)(textRect.Max.x, x);
        textRect.Min.y = (std::min)(textRect.Min.y, y);
        textRect.Max.y = (std::max)(textRect.Max.y, y + size);
        return textRect;
    }

    void Text::ConfigurePointerInteraction() const
    {
        ECS_WORLD(GetInternalWorld())

        const auto entity = GetEntity();
        GET(entity, physics::PointerCollisionListener);
        GET(entity, editor::SelectableByPointerTag);
    }
}
