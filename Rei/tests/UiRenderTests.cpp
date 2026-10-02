#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Modules/Render/UI/UiHierarchy.h"
#include "Modules/Render/UI/Text/TextGeometry.h"
#include "Modules/Resources/Serialization/BinaryReader.h"
#include "rei_behaviours/ui/Text.h"
#include "glad/glad.h"
#include <fstream>
#include <utility>
#include <chrono>
#include <cmath>

using rei::render::Font;

TEST_CASE("UI hierarchy snapshot keeps sibling order and separate roots", "[ui-render]")
{
    rei::render::UiHierarchy hierarchy;
    const rei::ecs::Entity root(1, 0), other(2, 0), first(3, 0), last(4, 0), nested(5, 0);
    hierarchy.AddChild(last, root, 20);
    hierarchy.AddChild(nested, first, 0);
    hierarchy.AddChild(first, root, -2);
    hierarchy.AddChild(root, rei::ecs::NULL_ENTITY, 0);
    hierarchy.AddChild(other, rei::ecs::NULL_ENTITY, 1);
    hierarchy.Sort();
    REQUIRE(hierarchy.GetChildren(root).size() == 2);
    REQUIRE(hierarchy.GetChildren(root)[0].Entity == first);
    REQUIRE(hierarchy.GetChildren(root)[1].Entity == last);
    REQUIRE(hierarchy.GetChildren(first)[0].Entity == nested);
    REQUIRE(hierarchy.GetChildren(other).empty());
    REQUIRE(hierarchy.GetChildren(nested).empty());
    REQUIRE(hierarchy.GetChildren(rei::ecs::Entity(1, 1)).empty());
}

TEST_CASE("Text quad keeps geometry and samples its atlas rectangle top to bottom", "[ui-render]")
{
    rei::render::FontGlyph glyph;
    glyph.UvMin = {0.2f, 0.3f};
    glyph.UvMax = {0.4f, 0.6f};
    const auto vertices = rei::render::text_geometry::MakeGlyphQuad(10, 20, 30, 40, glyph);
    const std::array<rei::math::Vector2, 6> positions = {{{10, 60}, {10, 20}, {40, 20}, {10, 60}, {40, 20}, {40, 60}}};
    for (std::size_t i = 0; i < positions.size(); ++i)
    {
        REQUIRE(vertices[i * 8] == positions[i].x);
        REQUIRE(vertices[i * 8 + 1] == positions[i].y);
        REQUIRE(vertices[i * 8 + 2] == 0);
        REQUIRE(vertices[i * 8 + 5] == 1);
        REQUIRE(vertices[i * 8 + 6] == (positions[i].x == 10 ? glyph.UvMin.x : glyph.UvMax.x));
        REQUIRE(vertices[i * 8 + 7] == (positions[i].y == 60 ? glyph.UvMin.y : glyph.UvMax.y));
    }
}

namespace
{
    template <typename T>
    struct GlOverride
    {
        T& Slot;
        T Previous;
        GlOverride(T& slot, T replacement) : Slot(slot), Previous(slot) { Slot = replacement; }
        ~GlOverride() { Slot = Previous; }
    };

    // Resource ownership/atlas pixels only. Real rendering is checked in the shared engine harness.
    struct AtlasGlSpy
    {
        inline static u32 NextId;
        inline static i32 Alignment, Width, Height, Uploads;
        inline static std::vector<u8> Pixels;
        inline static std::vector<u32> Deleted;
        AtlasGlSpy()
        {
            NextId = 1;
            Alignment = 8;
            Width = Height = Uploads = 0;
            Pixels.clear();
            Deleted.clear();
        }
        GlOverride<PFNGLGENTEXTURESPROC> Gen{glad_glGenTextures, +[](GLsizei count, GLuint* ids) { while (count--) *ids++ = NextId++; }};
        GlOverride<PFNGLDELETETEXTURESPROC> Delete{glad_glDeleteTextures, +[](GLsizei count, const GLuint* ids) { while (count--) Deleted.push_back(*ids++); }};
        GlOverride<PFNGLBINDTEXTUREPROC> Bind{glad_glBindTexture, +[](GLenum, GLuint) {}};
        GlOverride<PFNGLGETINTEGERVPROC> Get{glad_glGetIntegerv, +[](GLenum name, GLint* value) { REQUIRE(name == GL_UNPACK_ALIGNMENT); *value = Alignment; }};
        GlOverride<PFNGLPIXELSTOREIPROC> Store{glad_glPixelStorei, +[](GLenum name, GLint value) { REQUIRE(name == GL_UNPACK_ALIGNMENT); Alignment = value; }};
        GlOverride<PFNGLTEXPARAMETERIPROC> Parameter{glad_glTexParameteri, +[](GLenum, GLenum name, GLint value)
        {
            REQUIRE(value == ((name == GL_TEXTURE_WRAP_S || name == GL_TEXTURE_WRAP_T) ? GL_CLAMP_TO_EDGE : GL_LINEAR));
        }};
        GlOverride<PFNGLTEXIMAGE2DPROC> Image{glad_glTexImage2D, +[](GLenum target, GLint, GLint internal, GLsizei width, GLsizei height, GLint, GLenum format, GLenum type, const void* pixels)
        {
            REQUIRE(target == GL_TEXTURE_2D);
            REQUIRE(internal == GL_R8);
            REQUIRE(format == GL_RED);
            REQUIRE(type == GL_UNSIGNED_BYTE);
            REQUIRE(Alignment == 1);
            Width = width; Height = height; ++Uploads;
            const auto* bytes = static_cast<const u8*>(pixels);
            Pixels.assign(bytes, bytes + width * height);
        }};
    };

    std::filesystem::path TestFontPath()
    {
        return std::filesystem::path(__FILE__).parent_path().parent_path() / "resources/fonts/Roboto-Regular.ttf";
    }

    Font LoadTestFont()
    {
        std::ifstream input(TestFontPath(), std::ios::binary);
        REQUIRE(input.good());
        const std::vector<u8> bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
        const auto path = std::filesystem::temp_directory_path() / ("rei-ui-font-" + std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()) + ".bin");
        struct RemoveFile
        {
            std::filesystem::path Path;
            ~RemoveFile() { std::error_code error; std::filesystem::remove(Path, error); }
        } cleanup{path};
        {
            std::ofstream output(path, std::ios::binary);
            const i32 length = static_cast<i32>(bytes.size());
            output.write(reinterpret_cast<const char*>(&length), sizeof(length));
            output.write(reinterpret_cast<const char*>(bytes.data()), bytes.size());
            REQUIRE(output.good());
        }
        rei::resources::BinaryReader reader(path.string());
        return Font(reader);
    }
}

TEST_CASE("Font atlas preserves glyph metrics, coverage and clamp padding", "[ui-render]")
{
    AtlasGlSpy spy;
    const auto raster = Font::LoadAscii(TestFontPath(), 48);
    auto font = LoadTestFont();
    font.PostLoad();
    REQUIRE(AtlasGlSpy::Uploads == 1);
    REQUIRE(AtlasGlSpy::NextId == 2);
    REQUIRE(AtlasGlSpy::Alignment == 8);
    REQUIRE(font.GetAtlasTextureId() != 0);
    for (i32 code = 0; code < 128; ++code)
    {
        const auto key = static_cast<u8>(code);
        REQUIRE(font.HasGlyph(key) == raster.HasGlyph(key));
        if (!font.HasGlyph(key)) continue;
        const auto& glyph = font.GetGlyph(key);
        const auto& original = raster.GetGlyph(key);
        REQUIRE(glyph.Width == original.Width);
        REQUIRE(glyph.Height == original.Height);
        REQUIRE(glyph.BearingX == original.BearingX);
        REQUIRE(glyph.BearingY == original.BearingY);
        REQUIRE(glyph.Advance == original.Advance);
        REQUIRE(glyph.Bitmap.empty());
        if (original.Bitmap.empty())
        {
            REQUIRE(glyph.TextureId == 0);
            continue;
        }
        REQUIRE(glyph.TextureId == font.GetAtlasTextureId());
        const i32 x = static_cast<i32>(std::lround(glyph.UvMin.x * AtlasGlSpy::Width));
        const i32 y = static_cast<i32>(std::lround(glyph.UvMin.y * AtlasGlSpy::Height));
        REQUIRE(x >= 2);
        REQUIRE(y >= 2);
        REQUIRE(static_cast<i32>(std::lround(glyph.UvMax.x * AtlasGlSpy::Width)) == x + glyph.Width);
        REQUIRE(static_cast<i32>(std::lround(glyph.UvMax.y * AtlasGlSpy::Height)) == y + glyph.Height);
        REQUIRE(x + glyph.Width + 2 <= AtlasGlSpy::Width);
        REQUIRE(y + glyph.Height + 2 <= AtlasGlSpy::Height);
        bool pixelsMatch = true;
        for (i32 row = -2; row < glyph.Height + 2; ++row)
            for (i32 col = -2; col < glyph.Width + 2; ++col)
            {
                const auto source = original.Bitmap[std::clamp(row, 0, glyph.Height - 1) * glyph.Width + std::clamp(col, 0, glyph.Width - 1)];
                pixelsMatch &= AtlasGlSpy::Pixels[(y + row) * AtlasGlSpy::Width + x + col] == source;
            }
        REQUIRE(pixelsMatch);
    }
}

TEST_CASE("Font atlas survives reload and moves with one owning texture", "[ui-render]")
{
    AtlasGlSpy spy;
    {
        auto source = LoadTestFont();
        source.PostLoad();
        source.PostLoad();
        REQUIRE(AtlasGlSpy::Deleted == std::vector<u32>{1});
        REQUIRE(source.GetAtlasTextureId() == 2);
        Font moved(std::move(source));
        REQUIRE(source.GetAtlasTextureId() == 0);
        REQUIRE(moved.GetGlyph('A').TextureId == 2);
        auto destination = LoadTestFont();
        destination.PostLoad();
        destination = std::move(moved);
        REQUIRE(moved.GetAtlasTextureId() == 0);
        REQUIRE(AtlasGlSpy::Deleted == std::vector<u32>{1, 3});
        destination = std::move(destination);
        REQUIRE(destination.GetAtlasTextureId() == 2);
    }
    REQUIRE(AtlasGlSpy::Deleted == std::vector<u32>{1, 3, 2});
}

TEST_CASE("Atlas text retains selectable bounds outside its UI rectangle", "[ui-render]")
{
    AtlasGlSpy spy;
    rei::assets::AssetRef<Font> font("test-font");
    font.Record = std::make_shared<rei::assets::AssetRecord>();
    font.Record->Id = font.Id;
    font.Record->State = rei::assets::AssetState::Loaded;
    auto loaded = std::make_shared<Font>(LoadTestFont());
    loaded->PostLoad();
    font.Record->Value = loaded;
    rei::ui::Text text;
    text.SetFont(font);
    text.SetValue("AAA\nBBB");
    text.SetSize(48);
    text.SetAutoSize(false);
    const rei::math::Rect rect{{10, 10}, {20, 20}};
    const auto bounds = text.CalculateRenderRect(rect);
    REQUIRE(bounds.Max.x > rect.Max.x);
    REQUIRE(bounds.Min.y < rect.Min.y);
    REQUIRE(text.IsRaycastTarget());
}
