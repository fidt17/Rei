#pragma once

#include "Common/Primitives.h"
#include "Common/Math/Vector2.h"

#include <filesystem>
#include <string>
#include <unordered_map>
#include <vector>

namespace rei::resources
{
    class BinaryReader;
}

namespace rei::render
{
    constexpr i32 REI_DEFAULT_FONT_PIXEL_HEIGHT = 48;
    constexpr i32 REI_FONT_ADVANCE_FRACTION_BITS = 6;

    struct FontGlyph
    {
        // Borrowed atlas handle; zero for glyphs without a bitmap. Font owns the texture.
        u32 TextureId = 0;
        math::Vector2 UvMin{};
        math::Vector2 UvMax{};
        i32 Width = 0;
        i32 Height = 0;
        i32 BearingX = 0;
        i32 BearingY = 0;
        i32 Advance = 0;
        std::vector<u8> Bitmap{};

        f32 GetAdvancePixels() const;
    };

    class Font
    {
    public:
        REI_API Font() = default;
        REI_API explicit Font(resources::BinaryReader& reader);
        Font(const Font& other) = delete;
        Font& operator=(const Font& other) = delete;
        REI_API Font(Font&& other) noexcept;
        REI_API Font& operator=(Font&& other) noexcept;
        REI_API ~Font();

        REI_API static Font LoadAscii(const std::filesystem::path& fontPath, i32 pixelHeight);
        REI_API void PostLoad();

        REI_API const std::string& GetFamilyName() const;
        REI_API i32 GetPixelHeight() const;
        REI_API u32 GetAtlasTextureId() const;
        // Borrowed until the font is reloaded, moved or destroyed.
        REI_API const FontGlyph* FindGlyph(u8 character) const;
        REI_API const FontGlyph& GetGlyph(u8 character) const;
        REI_API bool HasGlyph(u8 character) const;

    private:
        std::string _familyName{};
        i32 _pixelHeight = REI_DEFAULT_FONT_PIXEL_HEIGHT;
        std::vector<u8> _fontData{};
        std::unordered_map<u8, FontGlyph> _glyphs{};
        u32 _atlasTexture = 0;

        void LoadAsciiFromMemory();
        void UploadAtlas();
        void DeleteAtlas();
    };
}
