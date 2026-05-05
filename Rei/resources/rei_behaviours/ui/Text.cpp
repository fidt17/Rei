#include "pch.h"

#include "Text.h"

namespace rei::ui
{
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
}
