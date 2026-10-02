#pragma once

#include <array>
#include "Font.h"

namespace rei::render::text_geometry
{
    inline std::array<f32, 48> MakeGlyphQuad(f32 x, f32 y, f32 width, f32 height, const FontGlyph& glyph)
    {
        const auto& min = glyph.UvMin;
        const auto& max = glyph.UvMax;
        return {
            x, y + height, 0.0f, 0.0f, 0.0f, 1.0f, min.x, min.y,
            x, y, 0.0f, 0.0f, 0.0f, 1.0f, min.x, max.y,
            x + width, y, 0.0f, 0.0f, 0.0f, 1.0f, max.x, max.y,
            x, y + height, 0.0f, 0.0f, 0.0f, 1.0f, min.x, min.y,
            x + width, y, 0.0f, 0.0f, 0.0f, 1.0f, max.x, max.y,
            x + width, y + height, 0.0f, 0.0f, 0.0f, 1.0f, max.x, min.y,
        };
    }
}
