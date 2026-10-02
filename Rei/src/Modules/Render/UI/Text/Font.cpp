#include "pch.h"
#include "Font.h"

#include <algorithm>
#include <utility>

#include "Modules/Resources/Serialization/BinaryReader.h"
#include "glad/glad.h"

#include <ft2build.h>
#include FT_FREETYPE_H

namespace
{
    struct LoadedAsciiFontData
    {
        std::string FamilyName{};
        i32 PixelHeight = 0;
        std::unordered_map<u8, rei::render::FontGlyph> Glyphs{};
    };

    std::vector<u8> CopyGlyphBitmap(const FT_Bitmap& bitmap)
    {
        const i32 width = static_cast<i32>(bitmap.width);
        const i32 height = static_cast<i32>(bitmap.rows);
        const i32 pitch = static_cast<i32>(std::abs(bitmap.pitch));
        std::vector<u8> result(width * height);

        for (i32 row = 0; row < height; row++)
        {
            const u8* sourceRow = bitmap.buffer + row * pitch;
            u8* targetRow = result.data() + row * width;
            std::copy(sourceRow, sourceRow + width, targetRow);
        }

        return result;
    }

    LoadedAsciiFontData LoadAsciiGlyphs(FT_Face face, const i32 pixelHeight)
    {
        if (FT_Set_Pixel_Sizes(face, 0, static_cast<FT_UInt>(pixelHeight)) != 0)
        {
            REI_THROW("Font pixel size setup failed")
        }

        LoadedAsciiFontData fontData{};
        fontData.Glyphs.reserve(128);

        for (u8 character = 0; character < 128; character++)
        {
            if (FT_Load_Char(face, character, FT_LOAD_RENDER) != 0)
            {
                continue;
            }

            rei::render::FontGlyph glyph{};
            glyph.Width = static_cast<i32>(face->glyph->bitmap.width);
            glyph.Height = static_cast<i32>(face->glyph->bitmap.rows);
            glyph.BearingX = face->glyph->bitmap_left;
            glyph.BearingY = face->glyph->bitmap_top;
            glyph.Advance = static_cast<i32>(face->glyph->advance.x);
            glyph.Bitmap = CopyGlyphBitmap(face->glyph->bitmap);

            fontData.Glyphs.insert({ character, std::move(glyph) });
        }

        fontData.FamilyName = face->family_name != nullptr ? face->family_name : "";
        fontData.PixelHeight = pixelHeight;

        REI_THROW_IF(!fontData.Glyphs.contains('A'), "Font ASCII glyph load failed")
        return fontData;
    }
}

f32 rei::render::FontGlyph::GetAdvancePixels() const
{
    return static_cast<f32>(Advance) / static_cast<f32>(1 << REI_FONT_ADVANCE_FRACTION_BITS);
}

rei::render::Font::Font(resources::BinaryReader& reader)
{
    i32 length = 0;
    u8* data = reader.GetBytes(length);
    _fontData = std::vector<u8>(data, data + length);
    delete[] data;
}

rei::render::Font::Font(Font&& other) noexcept
    : _familyName(std::move(other._familyName)),
      _pixelHeight(other._pixelHeight),
      _fontData(std::move(other._fontData)),
      _glyphs(std::move(other._glyphs)),
      _atlasTexture(std::exchange(other._atlasTexture, 0))
{
    other._glyphs.clear();
}

rei::render::Font& rei::render::Font::operator=(Font&& other) noexcept
{
    if (this == &other) return *this;

    DeleteAtlas();
    _familyName = std::move(other._familyName);
    _pixelHeight = other._pixelHeight;
    _fontData = std::move(other._fontData);
    _glyphs = std::move(other._glyphs);
    _atlasTexture = std::exchange(other._atlasTexture, 0);
    other._glyphs.clear();

    return *this;
}

rei::render::Font::~Font()
{
    DeleteAtlas();
}

rei::render::Font rei::render::Font::LoadAscii(const std::filesystem::path& fontPath, const i32 pixelHeight)
{
    REI_THROW_IF(fontPath.empty(), "Font path is empty")
    REI_THROW_IF(pixelHeight <= 0, "Font pixel height must be positive")

    FT_Library library = nullptr;
    if (FT_Init_FreeType(&library) != 0)
    {
        REI_THROW("FreeType initialization failed")
    }

    FT_Face face = nullptr;
    if (FT_New_Face(library, fontPath.string().c_str(), 0, &face) != 0)
    {
        FT_Done_FreeType(library);
        REI_THROW("Font load failed: " + fontPath.string())
    }

    Font font{};
    try
    {
        auto fontData = LoadAsciiGlyphs(face, pixelHeight);
        font._familyName = std::move(fontData.FamilyName);
        font._pixelHeight = fontData.PixelHeight;
        font._glyphs = std::move(fontData.Glyphs);
    }
    catch (...)
    {
        FT_Done_Face(face);
        FT_Done_FreeType(library);
        throw;
    }
    
    FT_Done_Face(face);
    FT_Done_FreeType(library);

    return font;
}

void rei::render::Font::PostLoad()
{
    DeleteAtlas();
    LoadAsciiFromMemory();
    UploadAtlas();
}

const std::string& rei::render::Font::GetFamilyName() const
{
    return _familyName;
}

i32 rei::render::Font::GetPixelHeight() const
{
    return _pixelHeight;
}

u32 rei::render::Font::GetAtlasTextureId() const
{
    return _atlasTexture;
}

const rei::render::FontGlyph* rei::render::Font::FindGlyph(const u8 character) const
{
    const auto found = _glyphs.find(character);
    return found == _glyphs.end() ? nullptr : &found->second;
}

const rei::render::FontGlyph& rei::render::Font::GetGlyph(const u8 character) const
{
    const auto* glyph = FindGlyph(character);
    REI_THROW_IF(glyph == nullptr, "Missing font glyph: " + STRING(character))
    return *glyph;
}

bool rei::render::Font::HasGlyph(const u8 character) const
{
    return FindGlyph(character) != nullptr;
}

void rei::render::Font::LoadAsciiFromMemory()
{
    REI_THROW_IF(_fontData.empty(), "Font data is empty")

    FT_Library library = nullptr;
    if (FT_Init_FreeType(&library) != 0)
    {
        REI_THROW("FreeType initialization failed")
    }

    FT_Face face = nullptr;
    if (FT_New_Memory_Face(library, _fontData.data(), static_cast<FT_Long>(_fontData.size()), 0, &face) != 0)
    {
        FT_Done_FreeType(library);
        REI_THROW("Font memory load failed")
    }

    try
    {
        auto fontData = LoadAsciiGlyphs(face, _pixelHeight);
        _familyName = std::move(fontData.FamilyName);
        _pixelHeight = fontData.PixelHeight;
        _glyphs = std::move(fontData.Glyphs);
    }
    catch (...)
    {
        FT_Done_Face(face);
        FT_Done_FreeType(library);
        throw;
    }

    FT_Done_Face(face);
    FT_Done_FreeType(library);
}

void rei::render::Font::UploadAtlas()
{
    constexpr i32 columns = 16;
    constexpr i32 rows = 8;
    constexpr i32 padding = 2;
    i32 maxWidth = 0;
    i32 maxHeight = 0;
    for (const auto& [_, glyph] : _glyphs)
    {
        maxWidth = (std::max)(maxWidth, glyph.Width);
        maxHeight = (std::max)(maxHeight, glyph.Height);
    }
    if (maxWidth <= 0 || maxHeight <= 0) return;

    const i32 cellWidth = maxWidth + padding * 2;
    const i32 cellHeight = maxHeight + padding * 2;
    const i32 atlasWidth = columns * cellWidth;
    const i32 atlasHeight = rows * cellHeight;
    std::vector<u8> pixels(static_cast<std::size_t>(atlasWidth) * atlasHeight, 0);
    for (auto& [character, glyph] : _glyphs)
    {
        if (glyph.Width <= 0 || glyph.Height <= 0 || glyph.Bitmap.empty()) continue;
        const i32 x = (character % columns) * cellWidth + padding;
        const i32 y = (character / columns) * cellHeight + padding;
        // Extruded edges preserve the old per-glyph CLAMP_TO_EDGE filtering.
        for (i32 row = -padding; row < glyph.Height + padding; ++row)
        {
            const i32 sourceRow = std::clamp(row, 0, glyph.Height - 1);
            for (i32 col = -padding; col < glyph.Width + padding; ++col)
            {
                const i32 sourceCol = std::clamp(col, 0, glyph.Width - 1);
                pixels[(y + row) * atlasWidth + x + col] = glyph.Bitmap[sourceRow * glyph.Width + sourceCol];
            }
        }
        glyph.UvMin = {static_cast<f32>(x) / atlasWidth, static_cast<f32>(y) / atlasHeight};
        glyph.UvMax = {static_cast<f32>(x + glyph.Width) / atlasWidth, static_cast<f32>(y + glyph.Height) / atlasHeight};
    }

    i32 unpackAlignment = 0;
    glGetIntegerv(GL_UNPACK_ALIGNMENT, &unpackAlignment);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glGenTextures(1, &_atlasTexture);
    glBindTexture(GL_TEXTURE_2D, _atlasTexture);
    glTexImage2D(GL_TEXTURE_2D, 0, GL_R8, atlasWidth, atlasHeight, 0, GL_RED, GL_UNSIGNED_BYTE, pixels.data());
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    glPixelStorei(GL_UNPACK_ALIGNMENT, unpackAlignment);

    for (auto& [_, glyph] : _glyphs)
    {
        if (glyph.Width > 0 && glyph.Height > 0 && !glyph.Bitmap.empty()) glyph.TextureId = _atlasTexture;
        glyph.Bitmap.clear();
        glyph.Bitmap.shrink_to_fit();
    }
}

void rei::render::Font::DeleteAtlas()
{
    if (_atlasTexture != 0) glDeleteTextures(1, &_atlasTexture);
    _atlasTexture = 0;
    for (auto& [_, glyph] : _glyphs) glyph.TextureId = 0;
}
