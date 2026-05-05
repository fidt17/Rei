#include "pch.h"
#include "Font.h"

#include <algorithm>

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
      _glyphs(std::move(other._glyphs))
{
    other._glyphs.clear();
}

rei::render::Font& rei::render::Font::operator=(Font&& other) noexcept
{
    if (this == &other) return *this;

    DeleteGlyphTextures();
    _familyName = std::move(other._familyName);
    _pixelHeight = other._pixelHeight;
    _fontData = std::move(other._fontData);
    _glyphs = std::move(other._glyphs);
    other._glyphs.clear();

    return *this;
}

rei::render::Font::~Font()
{
    DeleteGlyphTextures();
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
    LoadAsciiFromMemory();
    UploadGlyphTextures();
}

const std::string& rei::render::Font::GetFamilyName() const
{
    return _familyName;
}

i32 rei::render::Font::GetPixelHeight() const
{
    return _pixelHeight;
}

const rei::render::FontGlyph& rei::render::Font::GetGlyph(const u8 character) const
{
    REI_THROW_IF(!_glyphs.contains(character), "Missing font glyph: " + STRING(character))
    return _glyphs.at(character);
}

bool rei::render::Font::HasGlyph(const u8 character) const
{
    return _glyphs.contains(character);
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

void rei::render::Font::UploadGlyphTextures()
{
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);

    for (auto& [_, glyph] : _glyphs)
    {
        if (glyph.TextureId != 0) continue;
        if (glyph.Width <= 0 || glyph.Height <= 0 || glyph.Bitmap.empty()) continue;

        glGenTextures(1, &glyph.TextureId);
        glBindTexture(GL_TEXTURE_2D, glyph.TextureId);
        glTexImage2D(
            GL_TEXTURE_2D,
            0,
            GL_RED,
            glyph.Width,
            glyph.Height,
            0,
            GL_RED,
            GL_UNSIGNED_BYTE,
            glyph.Bitmap.data());

        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

        glyph.Bitmap.clear();
        glyph.Bitmap.shrink_to_fit();
    }

    glPixelStorei(GL_UNPACK_ALIGNMENT, 4);
}

void rei::render::Font::DeleteGlyphTextures()
{
    for (auto& [_, glyph] : _glyphs)
    {
        if (glyph.TextureId == 0) continue;

        glDeleteTextures(1, &glyph.TextureId);
        glyph.TextureId = 0;
    }
}
