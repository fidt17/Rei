#include "pch.h"
#include "support/NativeRenderFixture.h"
#include "Modules/Input/Input.h"
#include "Modules/Render/Modules/UIRenderModule.h"
#include "Modules/Render/RenderScenario/FrameBuffer.h"
#include "Modules/Resources/Serialization/BinaryReader.h"
#include "Modules/Components/ActiveTag.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/ui/Text.h"
#include <ft2build.h>
#include FT_FREETYPE_H

using namespace rei;
using namespace rei::tests;

namespace
{
    std::filesystem::path FontSource()
    {
        return std::filesystem::path(__FILE__).parent_path().parent_path() / "resources/fonts/Roboto-Regular.ttf";
    }

    class DirectFontMetrics
    {
    public:
        FT_Library Library = nullptr;
        FT_Face Face = nullptr;

        DirectFontMetrics()
        {
            if (FT_Init_FreeType(&Library) != 0) throw std::runtime_error("Independent FreeType initialization failed");
            if (FT_New_Face(Library, FontSource().string().c_str(), 0, &Face) != 0)
            {
                FT_Done_FreeType(Library);
                throw std::runtime_error("Bundled font unavailable to independent FreeType oracle");
            }
            if (FT_Set_Pixel_Sizes(Face, 0, 48) != 0)
            {
                FT_Done_Face(Face);
                FT_Done_FreeType(Library);
                throw std::runtime_error("Independent FreeType pixel-size setup failed");
            }
        }

        ~DirectFontMetrics() { FT_Done_Face(Face); FT_Done_FreeType(Library); }

        f32 Advance(const u8 character)
        {
            REQUIRE(FT_Load_Char(Face, character, FT_LOAD_RENDER) == 0);
            return static_cast<f32>(Face->glyph->advance.x) / 64.0f;
        }
    };

    render::Font PackedFont(TemporaryDirectory& files)
    {
        const auto source = ReadBytes(FontSource());
        REQUIRE_FALSE(source.empty());
        std::vector<u8> bytes;
        AppendInteger(bytes, source.size(), 4);
        bytes.insert(bytes.end(), source.begin(), source.end());
        const auto path = files.Write("owned-font.bin", bytes);
        resources::BinaryReader reader(path.string());
        return render::Font(reader);
    }

    class InstalledInputCallbacks
    {
    public:
        GLFWwindow* Window;
        GLFWkeyfun Key;
        GLFWmousebuttonfun Mouse;
        std::shared_ptr<api::EditorEventsRelay> Relay = std::make_shared<api::EditorEventsRelay>();
        std::vector<api::EditorInputEvent> Events;

        explicit InstalledInputCallbacks(GLFWwindow* window) : Window(window)
        {
            Services::GetInstance()->SetEditorEventsRelay(Relay);
            Relay->EditorInputReceivedEvent.append([this](const auto& value) { Events.push_back(value); });
            Input::SetSource(window);
            Key = glfwSetKeyCallback(window, nullptr);
            Mouse = glfwSetMouseButtonCallback(window, nullptr);
            REQUIRE(Key != nullptr);
            REQUIRE(Mouse != nullptr);
            glfwSetKeyCallback(window, Key);
            glfwSetMouseButtonCallback(window, Mouse);
        }

        ~InstalledInputCallbacks()
        {
            glfwSetKeyCallback(Window, nullptr);
            glfwSetMouseButtonCallback(Window, nullptr);
            glfwSetCursorPosCallback(Window, nullptr);
            glfwSetScrollCallback(Window, nullptr);
            Services::GetInstance()->SetEditorEventsRelay(nullptr);
        }
    };

    class TextRenderFixture
    {
    public:
        static constexpr i32 WIDTH = 64;
        static constexpr i32 HEIGHT = 128;
        NativeRenderFixture Native;
        render::FrameBuffer Target{WIDTH, HEIGHT};
        std::shared_ptr<render::CameraModule> Camera = std::make_shared<render::CameraModule>();
        std::unique_ptr<render::UIRenderModule> Ui;
        ecs::Entity CameraEntity = ecs::NULL_ENTITY;
        ecs::Entity CanvasEntity = ecs::NULL_ENTITY;
        ecs::Entity LabelEntity = ecs::NULL_ENTITY;

        TextRenderFixture()
        {
            auto& scene = Native.Scene;
            CameraEntity = scene.Entity(100);
            CanvasEntity = scene.Entity(101);
            LabelEntity = scene.Entity(102);
            auto& camera = scene.Registry->Get<render::Camera>(CameraEntity);
            camera = render::Camera(7301, CameraEntity);
            camera.SetOutputSize(WIDTH, HEIGHT);
            Camera->SetCamera(ecs::ComponentRef<render::Camera>(scene.Registry, CameraEntity));
            Camera->OnBeforeRender();
            auto& canvas = scene.Registry->Get<ui::Canvas>(CanvasEntity);
            canvas = ui::Canvas(7302, CanvasEntity);
            canvas.REI_SET({{"_scaleMode", {{"Value", static_cast<i32>(ui::ConstantPixelSize)}}}});
            scene.Registry->Get<ActiveTag>(CanvasEntity);
            auto& rect = scene.Registry->Get<ui::RectTransform>(LabelEntity);
            rect = ui::RectTransform(7303, LabelEntity);
            const auto vectorField = [](const std::string& name, const f32 x, const f32 y)
            {
                return nlohmann::json{{name, {{"Value", {{"x", {{"Value", x}}}, {"y", {{"Value", y}}}}}}}};
            };
            rect.REI_SET(vectorField("_anchorMin", 0, 1));
            rect.REI_SET(vectorField("_anchorMax", 0, 1));
            rect.REI_SET(vectorField("_pivot", 0, 1));
            rect.GetSizeDelta() = {WIDTH, HEIGHT};
            scene.Registry->Get<Transform>(LabelEntity).SetParent(CanvasEntity);
            auto& text = scene.Registry->Get<ui::Text>(LabelEntity);
            text = ui::Text(7305, LabelEntity);
            text.SetFont(scene.Assets->GetById<render::Font>("rei_roboto-regular.ttf"));
            text.SetColor(render::Color::Red());
            text.SetSize(50); // Declared line multiplier 1.2 gives exact 60-pixel baseline step.
            text.SetAutoSize(false);
            scene.Registry->Get<ActiveTag>(LabelEntity);
            scene.World->RefreshAll();
            Ui = std::make_unique<render::UIRenderModule>(Camera);
            Ui->Setup();
        }

        ~TextRenderFixture()
        {
            Ui.reset();
            Native.Scene.Registry->Del<ui::Text>(LabelEntity);
            Native.Scene.Registry->Del<ui::RectTransform>(LabelEntity);
            Native.Scene.Registry->Del<ui::Canvas>(CanvasEntity);
            Native.Scene.Registry->Del<render::Camera>(CameraEntity);
        }

        ui::Text& Text() { return Native.Scene.Registry->Get<ui::Text>(LabelEntity); }

        std::vector<u8> Render(const std::string& value)
        {
            Text().SetValue(value);
            Target.EnableBuffer(WIDTH, HEIGHT);
            REQUIRE(glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE);
            glViewport(0, 0, WIDTH, HEIGHT);
            glDisable(GL_SCISSOR_TEST);
            glDisable(GL_CULL_FACE);
            glClearColor(0, 0, 0, 1);
            glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            Ui->Render();
            std::vector<u8> result(WIDTH * HEIGHT * 4);
            glReadPixels(0, 0, WIDTH, HEIGHT, GL_RGBA, GL_UNSIGNED_BYTE, result.data());
            REQUIRE(glGetError() == GL_NO_ERROR);
            return result;
        }
    };
}

TEST_CASE("Input same-frame press and release keeps final state and both editor events", "[native][coverage][coverage-remaining][input][gl][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        InstalledInputCallbacks input(gl.Window());
        input.Key(gl.Window(), GLFW_KEY_A, 0, GLFW_PRESS, 0);
        input.Key(gl.Window(), GLFW_KEY_A, 0, GLFW_RELEASE, 0);
        input.Mouse(gl.Window(), GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        input.Mouse(gl.Window(), GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0);
        // Polling API compares final state with previous frame; relay preserves event edges.
        CHECK(Input::IsKeyUp(GLFW_KEY_A));
        CHECK_FALSE(Input::IsKeyPressed(GLFW_KEY_A));
        CHECK_FALSE(Input::IsKeyReleased(GLFW_KEY_A));
        CHECK(Input::IsMouseButtonUp(GLFW_MOUSE_BUTTON_LEFT));
        CHECK_FALSE(Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT));
        CHECK_FALSE(Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT));
        REQUIRE(input.Events.size() == 4);
        CHECK(input.Events[0].Type == api::KeyDown);
        CHECK(input.Events[1].Type == api::KeyUp);
        CHECK(input.Events[2].Type == api::MouseButtonDown);
        CHECK(input.Events[3].Type == api::MouseButtonUp);
    });
}

TEST_CASE("Input held release and repress in one frame has no polling edge", "[native][coverage][coverage-remaining][input][gl][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        InstalledInputCallbacks input(gl.Window());
        input.Key(gl.Window(), GLFW_KEY_A, 0, GLFW_PRESS, 0);
        Input::Update();
        input.Key(gl.Window(), GLFW_KEY_A, 0, GLFW_RELEASE, 0);
        input.Key(gl.Window(), GLFW_KEY_A, 0, GLFW_PRESS, 0);
        CHECK(Input::IsKeyDown(GLFW_KEY_A));
        CHECK_FALSE(Input::IsKeyPressed(GLFW_KEY_A));
        CHECK_FALSE(Input::IsKeyReleased(GLFW_KEY_A));
        REQUIRE(input.Events.size() == 3);
        CHECK(input.Events[1].Type == api::KeyUp);
        CHECK(input.Events[2].Type == api::KeyDown);
    });
}

TEST_CASE("Input focus metadata and GLFW release callbacks form current focus-loss contract", "[native][coverage][coverage-remaining][input][gl][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        InstalledInputCallbacks input(gl.Window());
        CHECK(glfwSetWindowFocusCallback(gl.Window(), nullptr) == nullptr);
        const bool focused = glfwGetWindowAttrib(gl.Window(), GLFW_FOCUSED) == GLFW_TRUE;
        REQUIRE_FALSE(focused); // Owned hidden window never steals desktop focus.
        CHECK(Input::GetMouseState().HasFocus == focused);
        input.Key(gl.Window(), GLFW_KEY_A, 0, GLFW_PRESS, 0);
        input.Mouse(gl.Window(), GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        Input::Update();
        CHECK(Input::IsKeyDown(GLFW_KEY_A));
        CHECK(Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT));
        // GLFW supplies releases on focus loss. Input has no separate focus-loss event API.
        input.Key(gl.Window(), GLFW_KEY_A, 0, GLFW_RELEASE, 0);
        input.Mouse(gl.Window(), GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0);
        CHECK(Input::IsKeyReleased(GLFW_KEY_A));
        CHECK(Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT));
        CHECK_FALSE(Input::IsKeyDown(GLFW_KEY_A));
        CHECK_FALSE(Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT));
        CHECK_FALSE(Input::GetMouseState().HasFocus);
    });
}

TEST_CASE("Real font atlas pixels and glyph metrics match independent FreeType source", "[native][coverage][coverage-remaining][text][gl][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        TemporaryDirectory files;
        DirectFontMetrics oracle;
        oracle.Advance('A');
        const auto* source = oracle.Face->glyph;
        const i32 sourceWidth = static_cast<i32>(source->bitmap.width);
        const i32 sourceHeight = static_cast<i32>(source->bitmap.rows);
        auto font = PackedFont(files);
        glPixelStorei(GL_UNPACK_ALIGNMENT, 8);
        font.PostLoad();
        const auto& glyph = font.GetGlyph('A');
        CHECK(glyph.Width == sourceWidth);
        CHECK(glyph.Height == sourceHeight);
        CHECK(glyph.BearingX == source->bitmap_left);
        CHECK(glyph.BearingY == source->bitmap_top);
        CHECK(glyph.Advance == source->advance.x);
        CHECK(glyph.Bitmap.empty());
        CHECK(glIsTexture(font.GetAtlasTextureId()) == GL_TRUE);
        i32 alignment = 0, width = 0, height = 0, format = 0;
        glGetIntegerv(GL_UNPACK_ALIGNMENT, &alignment);
        CHECK(alignment == 8);
        glBindTexture(GL_TEXTURE_2D, font.GetAtlasTextureId());
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_WIDTH, &width);
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_HEIGHT, &height);
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_INTERNAL_FORMAT, &format);
        REQUIRE(width > 0);
        REQUIRE(height > 0);
        CHECK(format == GL_R8);
        glPixelStorei(GL_PACK_ALIGNMENT, 1);
        std::vector<u8> pixels(static_cast<size_t>(width) * height);
        glGetTexImage(GL_TEXTURE_2D, 0, GL_RED, GL_UNSIGNED_BYTE, pixels.data());
        const i32 x = static_cast<i32>(std::lround(glyph.UvMin.x * width));
        const i32 y = static_cast<i32>(std::lround(glyph.UvMin.y * height));
        REQUIRE(x >= 2);
        REQUIRE(y >= 2);
        REQUIRE(x + sourceWidth + 2 <= width);
        REQUIRE(y + sourceHeight + 2 <= height);
        bool equal = true;
        for (i32 row = -2; row < sourceHeight + 2; ++row)
            for (i32 col = -2; col < sourceWidth + 2; ++col)
            {
                const auto* sourceRow = source->bitmap.buffer + std::clamp(row, 0, sourceHeight - 1) * source->bitmap.pitch;
                equal &= pixels[(y + row) * width + x + col] == sourceRow[std::clamp(col, 0, sourceWidth - 1)];
            }
        CHECK(equal);
        CHECK(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("Real font atlas reload move assignment and destruction release driver objects", "[native][coverage][coverage-remaining][text][gl][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        TemporaryDirectory files;
        u32 survivor = 0;
        {
            auto font = PackedFont(files);
            font.PostLoad();
            const auto original = font.GetAtlasTextureId();
            REQUIRE(glIsTexture(original) == GL_TRUE);
            font.PostLoad();
            const auto reloaded = font.GetAtlasTextureId();
            REQUIRE(reloaded != 0);
            // If GL reuses numeric ID, valid new storage still owns one atlas.
            if (reloaded != original) CHECK(glIsTexture(original) == GL_FALSE);
            CHECK(font.GetGlyph('A').TextureId == reloaded);
            render::Font moved(std::move(font));
            CHECK(font.GetAtlasTextureId() == 0);
            CHECK_FALSE(font.HasGlyph('A'));
            CHECK(moved.GetAtlasTextureId() == reloaded);
            CHECK(glIsTexture(reloaded) == GL_TRUE);
            auto destination = PackedFont(files);
            destination.PostLoad();
            const auto replaced = destination.GetAtlasTextureId();
            REQUIRE(replaced != reloaded);
            destination = std::move(moved);
            CHECK(moved.GetAtlasTextureId() == 0);
            CHECK(glIsTexture(replaced) == GL_FALSE);
            CHECK(destination.GetAtlasTextureId() == reloaded);
            destination = std::move(destination);
            survivor = destination.GetAtlasTextureId();
            CHECK(survivor == reloaded);
        }
        CHECK(glIsTexture(survivor) == GL_FALSE);
        CHECK(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("Text newline bounds and autosize use independent glyph advance and analytic line height", "[native][coverage][coverage-remaining][text][gl][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        DirectFontMetrics oracle;
        const f32 advance = oracle.Advance('A');
        ui::Text text;
        text.SetFont(fixture.Scene.Assets->GetById<render::Font>("rei_roboto-regular.ttf"));
        text.SetSize(48);
        text.SetValue("A\nA");
        const math::Rect box{{0, 0}, {100, 120}};
        const auto bounds = text.CalculateRenderRect(box);
        CHECK(bounds.Min.x == 0);
        CHECK(bounds.Max.x == Catch::Approx(advance).margin(1e-4f));
        CHECK(bounds.Min.y == Catch::Approx(14.4f).margin(1e-4f));
        CHECK(bounds.Max.y == 120);
        text.SetAutoSize(true);
        const math::Rect halfHeight{{0, 0}, {100, 52.8f}};
        CHECK(text.GetRenderSize(halfHeight) == Catch::Approx(24).margin(1e-4f));
        CHECK(text.CalculateRenderRect(halfHeight).GetSize().y == Catch::Approx(52.8f).margin(1e-4f));
        text.SetValue("    ");
        const f32 spaceWidth = 4 * oracle.Advance(' ');
        CHECK(text.GetRenderSize({{0, 0}, {spaceWidth / 2, 100}}) == Catch::Approx(24).margin(1e-4f));
        CHECK(text.GetRenderSize({{0, 0}, {0, 0}}) == 48);
    });
}

TEST_CASE("Actual UI text renderer skips missing glyph bytes and whitespace produces no pixels", "[native][coverage][coverage-remaining][text][gl][isolated]")
{
    IsolatedGl([]
    {
        TextRenderFixture fixture;
        const auto normal = fixture.Render("AA");
        const auto missing = fixture.Render(std::string("A") + static_cast<char>(255) + "A");
        CHECK(missing == normal);
        CHECK(std::ranges::any_of(normal, [](const u8 value) { return value != 0 && value != 255; }));
        const auto blank = fixture.Render(" \n ");
        bool allBlack = true;
        for (size_t offset = 0; offset < blank.size(); offset += 4)
            allBlack &= blank[offset] == 0 && blank[offset + 1] == 0 && blank[offset + 2] == 0;
        CHECK(allBlack);
        CHECK(fixture.Render("") == blank);
    });
}

TEST_CASE("Actual UI text glyph pixels preserve independent FreeType coverage and vertical orientation", "[native][coverage][coverage-remaining][text][gl][isolated]")
{
    IsolatedGl([]
    {
        TextRenderFixture fixture;
        DirectFontMetrics oracle;
        oracle.Advance('A');
        const auto* source = oracle.Face->glyph;
        const i32 width = static_cast<i32>(source->bitmap.width);
        const i32 height = static_cast<i32>(source->bitmap.rows);
        const i32 originX = source->bitmap_left;
        const i32 originY = TextRenderFixture::HEIGHT - 48 - height + source->bitmap_top;
        fixture.Text().SetSize(48);
        const auto pixels = fixture.Render("A");
        bool equal = true;
        u32 coverage = 0;
        for (i32 y = 0; y < TextRenderFixture::HEIGHT; ++y)
            for (i32 x = 0; x < TextRenderFixture::WIDTH; ++x)
            {
                u8 expected = 0;
                const i32 glyphX = x - originX;
                const i32 glyphY = y - originY;
                if (glyphX >= 0 && glyphX < width && glyphY >= 0 && glyphY < height)
                {
                    // FBO rows start at bottom; FreeType rows start at glyph top.
                    expected = source->bitmap.buffer[(height - glyphY - 1) * source->bitmap.pitch + glyphX];
                    if (expected) ++coverage;
                }
                const auto offset = (static_cast<size_t>(y) * TextRenderFixture::WIDTH + x) * 4;
                equal &= std::abs(static_cast<i32>(pixels[offset]) - expected) <= 1;
                equal &= pixels[offset + 1] == 0 && pixels[offset + 2] == 0;
            }
        REQUIRE(coverage > 0);
        CHECK(equal);
    });
}

TEST_CASE("Actual UI text renderer resets X and advances baseline 60 pixels at newline", "[native][coverage][coverage-remaining][text][gl][isolated]")
{
    IsolatedGl([]
    {
        TextRenderFixture fixture;
        const auto oneLine = fixture.Render("A");
        const auto twoLines = fixture.Render("A\nA");
        bool matches = true;
        u32 secondLineCoverage = 0;
        for (i32 y = 0; y < TextRenderFixture::HEIGHT; ++y)
            for (i32 x = 0; x < TextRenderFixture::WIDTH; ++x)
            {
                const auto offset = (static_cast<size_t>(y) * TextRenderFixture::WIDTH + x) * 4;
                const u8 shifted = y + 60 < TextRenderFixture::HEIGHT ? oneLine[(static_cast<size_t>(y + 60) * TextRenderFixture::WIDTH + x) * 4] : 0;
                if (shifted) ++secondLineCoverage;
                matches &= std::abs(static_cast<i32>(twoLines[offset]) - static_cast<i32>(oneLine[offset]) - shifted) <= 1;
                matches &= twoLines[offset + 1] == 0 && twoLines[offset + 2] == 0;
            }
        CHECK(secondLineCoverage > 0);
        CHECK(matches);
    });
}
