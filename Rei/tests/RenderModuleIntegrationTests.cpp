#include "pch.h"
#include "catch_amalgamated.hpp"
#include "support/NativeRenderFixture.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Render/Modules/UIRenderModule.h"
#include "Modules/Render/Modules/LightingRenderModule.h"
#include "Modules/Render/Modules/PostProcessingModule.h"
#include "Modules/Render/RenderScenario/DefaultRenderScenario.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/Image.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/ui/Text.h"
#include <thread>

using namespace rei;
using namespace rei::tests;

namespace
{
    std::shared_ptr<render::CameraModule> MakeCamera(NativeRenderFixture& fixture)
    {
        const auto entity = fixture.Scene.Entity(100);
        fixture.Scene.Add(entity, 7301, false);
        auto& camera = fixture.Scene.Registry->Get<render::Camera>(entity);
        camera.SetOutputSize(32, 32);
        auto module = std::make_shared<render::CameraModule>();
        module->SetCamera(ecs::ComponentRef<render::Camera>(fixture.Scene.Registry, entity));
        module->OnBeforeRender();
        return module;
    }

    ecs::Entity MakeCanvas(NativeRenderFixture& fixture, const i32 sceneId = 101)
    {
        const auto entity = fixture.Scene.Entity(sceneId);
        fixture.Scene.Add(entity, 7302, false);
        fixture.Scene.Registry->Get<ui::Canvas>(entity).REI_SET(SerializedField("_scaleMode", static_cast<i32>(ui::ConstantPixelSize)));
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        return entity;
    }

    ecs::Entity MakeImage(NativeRenderFixture& fixture, const ecs::Entity canvas, const render::Color& color, const i32 sceneId = 102)
    {
        const auto entity = fixture.Scene.Entity(sceneId);
        fixture.Scene.Registry->Get<Transform>(entity).SetParent(canvas);
        fixture.Scene.Add(entity, 7304, false);
        fixture.Scene.Registry->Get<ui::RectTransform>(entity).GetSizeDelta() = {32, 32};
        auto& image = fixture.Scene.Registry->Get<ui::Image>(entity);
        image.Init();
        image.SetColor(color);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        return entity;
    }

    void PrepareUiDraw(NativeRenderFixture& fixture, render::FrameBuffer& target)
    {
        fixture.Scene.World->Refresh();
        fixture.Scene.World->RefreshAll();
        target.EnableBuffer(32, 32);
        glViewport(0, 0, 32, 32);
        glDisable(GL_DEPTH_TEST);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glClearColor(0, 0, 0, 1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
    }

    f32 ReadFloatUniform(const render::Shader& shader, const std::string& name)
    {
        shader.Use();
        i32 program = 0;
        glGetIntegerv(GL_CURRENT_PROGRAM, &program);
        const auto location = shader.GetLocation(name);
        REQUIRE(location >= 0);
        f32 value = 0;
        glGetUniformfv(program, location, &value);
        return value;
    }

    i32 ReadPointLightCount(const render::Shader& shader)
    {
        shader.Use();
        i32 program = 0;
        glGetIntegerv(GL_CURRENT_PROGRAM, &program);
        const auto location = shader.GetLocation("_PointLightsCount");
        REQUIRE(location >= 0);
        i32 value = -1;
        glGetUniformiv(program, location, &value);
        return value;
    }

    void RequireEmptyLightSlots(const render::Shader& shader, const i32 start)
    {
        shader.Use();
        i32 program = 0;
        glGetIntegerv(GL_CURRENT_PROGRAM, &program);
        for (i32 i = start; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
        {
            const auto slot = "_PointLights[" + std::to_string(i) + "]";
            REQUIRE(ReadFloatUniform(shader, slot + ".Strength") == 0);
            std::array<f32, 3> position{};
            std::array<f32, 4> color{};
            const auto positionLocation = shader.GetLocation(slot + ".Position");
            const auto colorLocation = shader.GetLocation(slot + ".Color");
            REQUIRE(positionLocation >= 0);
            REQUIRE(colorLocation >= 0);
            glGetUniformfv(program, positionLocation, position.data());
            glGetUniformfv(program, colorLocation, color.data());
            REQUIRE(position == std::array<f32, 3>{0, 0, 0});
            REQUIRE(color == std::array<f32, 4>{0, 0, 0, 1});
        }
    }
}

TEST_CASE("RENDER01 UI module renders Image color through actual material and shader", "[native][coverage][coverage-remaining][gl][ui-render][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        const auto canvas = MakeCanvas(fixture);
        MakeImage(fixture, canvas, render::Color(1, 0, 0, 1));
        render::UIRenderModule module(camera);
        module.Setup();
        render::FrameBuffer target(32, 32);
        PrepareUiDraw(fixture, target);
        module.Render();
        RequirePixel(ReadPixel(), {255, 0, 0, 255});
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("RENDER02 sibling UI order and reordering change visible top Image", "[native][coverage][coverage-remaining][gl][ui-render][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        const auto canvas = MakeCanvas(fixture);
        const auto red = MakeImage(fixture, canvas, render::Color(1, 0, 0, 1), 102);
        const auto green = MakeImage(fixture, canvas, render::Color(0, 1, 0, 1), 103);
        render::UIRenderModule module(camera);
        module.Setup();
        render::FrameBuffer target(32, 32);
        PrepareUiDraw(fixture, target);
        module.Render();
        RequirePixel(ReadPixel(), {0, 255, 0, 255});
        fixture.Scene.Registry->Get<Transform>(red).SetChildOrder(1);
        PrepareUiDraw(fixture, target);
        module.Render();
        RequirePixel(ReadPixel(), {255, 0, 0, 255});
        REQUIRE(fixture.Scene.Registry->Get<Transform>(green).GetChildOrder() == 0);
    });
}

TEST_CASE("RENDER03 nested Canvas Image alpha contributes once to framebuffer", "[native][coverage][coverage-remaining][gl][ui-render][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        const auto outer = MakeCanvas(fixture, 101);
        const auto inner = MakeCanvas(fixture, 102);
        fixture.Scene.Registry->Get<Transform>(inner).SetParent(outer);
        MakeImage(fixture, inner, render::Color(1, 0, 0, 0.5f), 103);
        render::UIRenderModule module(camera);
        module.Setup();
        render::FrameBuffer target(32, 32);
        PrepareUiDraw(fixture, target);
        module.Render();
        const auto pixel = ReadPixel();
        INFO("Linear alpha blending: duplicate canvas traversal gives sRGB red 225; one image gives 188");
        REQUIRE(std::abs(static_cast<i32>(pixel[0]) - 188) <= 1);
        REQUIRE(pixel[1] == 0);
        REQUIRE(pixel[2] == 0);
    });
}

TEST_CASE("RENDER04 UI disable event and Image disable suppress pixels", "[native][coverage][coverage-remaining][gl][ui-render][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        const auto canvas = MakeCanvas(fixture);
        const auto image = MakeImage(fixture, canvas, render::Color(1, 0, 0, 1));
        render::UIRenderModule module(camera);
        module.Setup();
        render::FrameBuffer target(32, 32);
        SECTION("module") { fixture.Relay->UiRenderingEnabledReceivedEvent(false); }
        SECTION("image") { fixture.Scene.Registry->Get<ui::Image>(image).Disable(); }
        PrepareUiDraw(fixture, target);
        module.Render();
        RequirePixel(ReadPixel(), {0, 0, 0, 255});
    });
}

TEST_CASE("RENDER05 inactive Canvas ancestor suppresses nested active Image pixels", "[native][coverage][coverage-remaining][gl][ui-render][isolated][proposed-policy]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        const auto outer = MakeCanvas(fixture, 101);
        const auto inner = MakeCanvas(fixture, 102);
        fixture.Scene.Registry->Get<Transform>(inner).SetParent(outer);
        MakeImage(fixture, inner, render::Color(1, 0, 0, 1), 103);
        fixture.Scene.Registry->Del<ActiveTag>(outer);
        render::UIRenderModule module(camera);
        module.Setup();
        render::FrameBuffer target(32, 32);
        PrepareUiDraw(fixture, target);
        module.Render();
        RequirePixel(ReadPixel(), {0, 0, 0, 255});
    });
}

TEST_CASE("RENDER06 actual text glyph atlas produces pixels and whitespace produces none", "[native][coverage][coverage-remaining][gl][ui-render][font][isolated][proposed-policy]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        const auto canvas = MakeCanvas(fixture);
        const auto entity = fixture.Scene.Entity(102);
        fixture.Scene.Registry->Get<Transform>(entity).SetParent(canvas);
        fixture.Scene.Add(entity, 7305, false);
        auto& text = fixture.Scene.Registry->Get<ui::Text>(entity);
        text.LoadAssets(*fixture.Scene.Assets);
        text.SetSize(24);
        text.SetValue("A");
        fixture.Scene.Registry->Get<ui::RectTransform>(entity).GetSizeDelta() = {32, 32};
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        render::UIRenderModule module(camera);
        module.Setup();
        render::FrameBuffer target(32, 32);
        const auto whitePixels = [&]
        {
            std::array<u8, 32 * 32 * 4> pixels{};
            glReadPixels(0, 0, 32, 32, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
            i32 count = 0;
            for (u32 i = 0; i < pixels.size(); i += 4) if (pixels[i] != 0) ++count;
            return count;
        };
        PrepareUiDraw(fixture, target);
        module.Render();
        REQUIRE(whitePixels() > 10);
        text.SetValue(" \t\n");
        PrepareUiDraw(fixture, target);
        module.Render();
        REQUIRE(whitePixels() == 0);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("RENDER07 removed point lights reset stale shader strengths on next render", "[native][coverage][coverage-remaining][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        render::LightingRenderModule lighting(camera);
        const auto entity = fixture.Scene.Entity(101);
        fixture.Scene.Add(entity, 7306, false);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        fixture.Scene.Registry->Get<render::PointLight>(entity).SetStrength(0.75f);
        fixture.Scene.World->Refresh();
        lighting.OnBeforeRender();
        lighting.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Strength") == 0.75f);
        fixture.Scene.Registry->Del<render::PointLight>(entity);
        fixture.Scene.World->Refresh();
        lighting.OnBeforeRender();
        lighting.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Strength") == 0.0f);
    });
}

TEST_CASE("RENDER08 all four point-light shader slots receive distinct native strengths", "[native][coverage][coverage-remaining][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        render::LightingRenderModule lighting(camera);
        for (i32 i = 0; i < 4; ++i)
        {
            const auto entity = fixture.Scene.Entity(101 + i);
            fixture.Scene.Add(entity, 7306, false);
            fixture.Scene.Registry->Get<ActiveTag>(entity);
            fixture.Scene.Registry->Get<render::PointLight>(entity).SetStrength(0.1f * static_cast<f32>(i + 1));
        }
        fixture.Scene.World->Refresh();
        lighting.OnBeforeRender();
        lighting.SetLightValues(*shader.Get());
        for (i32 i = 0; i < 4; ++i) REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[" + std::to_string(i) + "].Strength") == 0.1f * static_cast<f32>(i + 1));
        REQUIRE(shader->GetLocation("_PointLights[4].Strength") == -1);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("RENDER12 removed ambient light resets native shader strength", "[native][coverage][coverage-remaining][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        render::LightingRenderModule lighting(camera);
        const auto entity = fixture.Scene.Entity(101);
        fixture.Scene.Add(entity, 7307, false);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        fixture.Scene.Registry->Get<render::AmbientLight>(entity).REI_SET(SerializedField("_strength", 0.5f));
        fixture.Scene.World->Refresh();
        lighting.OnBeforeRender();
        lighting.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == 0.5f);
        fixture.Scene.Registry->Del<render::AmbientLight>(entity);
        fixture.Scene.World->Refresh();
        lighting.OnBeforeRender();
        lighting.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == 0.0f);
    });
}

TEST_CASE("RENDER13 disabled point light contributes zero native shader strength", "[native][coverage][coverage-remaining][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        render::LightingRenderModule lighting(camera);
        const auto entity = fixture.Scene.Entity(101);
        fixture.Scene.Add(entity, 7306, false);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        fixture.Scene.Registry->Get<render::PointLight>(entity).SetStrength(0.75f);
        fixture.Scene.Registry->Get<render::PointLight>(entity).Disable();
        fixture.Scene.World->Refresh();
        lighting.OnBeforeRender();
        lighting.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Strength") == 0.0f);
    });
}

TEST_CASE("RENDER14 point light count caps at four and clears every unused native slot", "[native][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        render::LightingRenderModule lighting(MakeCamera(fixture));
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        std::vector<ecs::Entity> lights(5, ecs::NULL_ENTITY);
        const auto verify = [&](const i32 expected)
        {
            fixture.Scene.World->Refresh();
            lighting.OnBeforeRender();
            lighting.SetLightValues(*shader.Get());
            REQUIRE(ReadPointLightCount(*shader.Get()) == expected);
            RequireEmptyLightSlots(*shader.Get(), expected);
            REQUIRE(glGetError() == GL_NO_ERROR);
        };
        verify(0);
        for (i32 i = 0; i < 5; ++i)
        {
            lights[i] = fixture.Scene.Entity(101 + i);
            fixture.Scene.Add(lights[i], 7306, false);
            fixture.Scene.Registry->Get<ActiveTag>(lights[i]);
            fixture.Scene.Registry->Get<Transform>(lights[i]).GetLocalPosition() = {1, 2, 3};
            auto& light = fixture.Scene.Registry->Get<render::PointLight>(lights[i]);
            light.SetStrength(0.25f);
            light.SetColor(render::Color::Red());
            verify(std::min(i + 1, 4));
        }
        REQUIRE(shader->GetLocation("_PointLights[4].Strength") == -1);
        for (i32 i = 4; i >= 0; --i)
        {
            fixture.Scene.Registry->Del<render::PointLight>(lights[i]);
            verify(std::min(i, 4));
        }
    });
}

TEST_CASE("RENDER15 point light component and object activity clear and restore native values", "[native][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        render::LightingRenderModule lighting(MakeCamera(fixture));
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        const auto entity = fixture.Scene.Entity(101);
        fixture.Scene.Add(entity, 7306, false);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        auto& light = fixture.Scene.Registry->Get<render::PointLight>(entity);
        light.SetStrength(0.75f);
        const auto verify = [&](const i32 count)
        {
            fixture.Scene.World->Refresh();
            lighting.OnBeforeRender();
            lighting.SetLightValues(*shader.Get());
            REQUIRE(ReadPointLightCount(*shader.Get()) == count);
            if (count == 0) RequireEmptyLightSlots(*shader.Get(), 0);
            else REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Strength") == 0.75f);
        };
        verify(1);
        light.Disable();
        verify(0);
        light.Enable();
        verify(1);
        fixture.Scene.Registry->Del<ActiveTag>(entity);
        verify(0);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        verify(1);
    });
}

TEST_CASE("RENDER16 ambient selects first enabled active source and clears previous selection", "[native][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        render::LightingRenderModule lighting(MakeCamera(fixture));
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        const auto first = fixture.Scene.Entity(101);
        const auto second = fixture.Scene.Entity(102);
        fixture.Scene.Add(first, 7307, false);
        fixture.Scene.Add(second, 7307, false);
        fixture.Scene.Registry->Get<ActiveTag>(first);
        fixture.Scene.Registry->Get<ActiveTag>(second);
        auto& firstLight = fixture.Scene.Registry->Get<render::AmbientLight>(first);
        auto& secondLight = fixture.Scene.Registry->Get<render::AmbientLight>(second);
        firstLight.REI_SET(SerializedField("_strength", 0.75f));
        secondLight.REI_SET(SerializedField("_strength", 0.25f));
        const auto verify = [&](const f32 strength)
        {
            fixture.Scene.World->Refresh();
            lighting.OnBeforeRender();
            lighting.SetLightValues(*shader.Get());
            REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == strength);
        };
        firstLight.Disable();
        verify(0.25f);
        firstLight.Enable();
        verify(0.75f);
        fixture.Scene.Registry->Del<ActiveTag>(first);
        verify(0.25f);
        secondLight.Disable();
        verify(0);
        fixture.Scene.Registry->Del<render::AmbientLight>(second);
        verify(0);
    });
}

TEST_CASE("RENDER09 overlay postprocessing preserves flat input color", "[native][coverage][coverage-remaining][gl][postprocessing][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        render::PostProcessingModule module(camera);
        module.Setup();
        render::FrameBuffer source(32, 32);
        glClearColor(0.25f, 0.5f, 0.75f, 1);
        glClear(GL_COLOR_BUFFER_BIT);
        render::FrameBuffer target(32, 32);
        glDisable(GL_DEPTH_TEST);
        module.Render(source);
        RequirePixel(ReadPixel(), {137, 188, 225, 255});
    });
}

TEST_CASE("RENDER10 grayscale postprocessing outputs weighted luminance", "[native][coverage][coverage-remaining][gl][postprocessing][isolated][proposed-policy]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        camera->GetCamera().Get().SetRenderMode(Grayscale);
        render::PostProcessingModule module(camera);
        module.Setup();
        render::FrameBuffer source(32, 32);
        glClearColor(1, 0, 0, 1);
        glClear(GL_COLOR_BUFFER_BIT);
        render::FrameBuffer target(32, 32);
        glDisable(GL_DEPTH_TEST);
        module.Render(source);
        RequirePixel(ReadPixel(), {127, 127, 127, 255});
    });
}

TEST_CASE("RENDER11 inversion postprocessing outputs component complements", "[native][coverage][coverage-remaining][gl][postprocessing][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        camera->GetCamera().Get().SetRenderMode(Inversion);
        render::PostProcessingModule module(camera);
        module.Setup();
        render::FrameBuffer source(32, 32);
        glClearColor(1, 0.25f, 0, 1);
        glClear(GL_COLOR_BUFFER_BIT);
        render::FrameBuffer target(32, 32);
        glDisable(GL_DEPTH_TEST);
        module.Render(source);
        RequirePixel(ReadPixel(), {0, 225, 255, 255});
    });
}

TEST_CASE("CAPTURE01 frame capture accepts one pending callback and rejects empty or busy", "[native][coverage][coverage-remaining][gl][capture][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const std::vector<std::unique_ptr<render::CustomRenderModule>> custom;
        render::DefaultRenderScenario scenario(fixture.Gl.Window(), custom);
        i32 called = 0;
        REQUIRE_FALSE(scenario.RequestFrameCapture({}));
        REQUIRE(scenario.RequestFrameCapture([&](const u8*, i32, i32) { ++called; }));
        REQUIRE_FALSE(scenario.RequestFrameCapture([&](const u8*, i32, i32) { ++called; }));
        scenario.Dispose();
        REQUIRE(called == 1);
    });
}

TEST_CASE("CAPTURE02 no-camera frame callback reads current dimensions and black pixels once", "[native][coverage][coverage-remaining][gl][capture][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const std::vector<std::unique_ptr<render::CustomRenderModule>> custom;
        render::DefaultRenderScenario scenario(fixture.Gl.Window(), custom);
        i32 called = 0, width = 0, height = 0;
        i32 expectedWidth = 0, expectedHeight = 0;
        glfwGetFramebufferSize(fixture.Gl.Window(), &expectedWidth, &expectedHeight);
        std::vector<u8> image;
        REQUIRE(scenario.RequestFrameCapture([&](const u8* pixels, const i32 w, const i32 h)
        {
            ++called; width = w; height = h;
            if (pixels) image.assign(pixels, pixels + w * h * 4);
        }));
        glBindFramebuffer(GL_FRAMEBUFFER, 0);
        scenario.RenderWithoutCamera();
        scenario.RenderWithoutCamera();
        scenario.Dispose();
        REQUIRE(called == 1);
        REQUIRE(width == expectedWidth);
        REQUIRE(height == expectedHeight);
        REQUIRE(image.size() == static_cast<size_t>(expectedWidth) * expectedHeight * 4);
        for (u32 i = 0; i < image.size(); i += 4)
        {
            REQUIRE(image[i] == 0);
            REQUIRE(image[i + 1] == 0);
            REQUIRE(image[i + 2] == 0);
            REQUIRE(image[i + 3] == 255);
        }
    });
}

TEST_CASE("CAPTURE03 Dispose cancels pending capture exactly once and closes admission", "[native][coverage][coverage-remaining][gl][capture][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const std::vector<std::unique_ptr<render::CustomRenderModule>> custom;
        render::DefaultRenderScenario scenario(fixture.Gl.Window(), custom);
        i32 called = 0;
        REQUIRE(scenario.RequestFrameCapture([&](const u8* pixels, const i32 w, const i32 h)
        {
            ++called;
            CHECK(pixels == nullptr); CHECK(w == 0); CHECK(h == 0);
        }));
        scenario.Dispose();
        scenario.Dispose();
        REQUIRE(called == 1);
        REQUIRE_FALSE(scenario.RequestFrameCapture([](const u8*, i32, i32) {}));
    });
}

TEST_CASE("CAPTURE04 concurrent requests admit exactly one native callback", "[native][coverage][coverage-remaining][gl][capture][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const std::vector<std::unique_ptr<render::CustomRenderModule>> custom;
        render::DefaultRenderScenario scenario(fixture.Gl.Window(), custom);
        std::atomic<i32> accepted = 0, called = 0;
        std::vector<std::thread> threads;
        for (i32 i = 0; i < 8; ++i) threads.emplace_back([&]
        {
            if (scenario.RequestFrameCapture([&](const u8*, i32, i32) { ++called; })) ++accepted;
        });
        for (auto& thread : threads) thread.join();
        REQUIRE(accepted == 1);
        scenario.Dispose();
        REQUIRE(called == 1);
    });
}

TEST_CASE("CAPTURE05 no-camera capture follows resized hidden window dimensions", "[native][coverage][coverage-remaining][gl][capture][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const std::vector<std::unique_ptr<render::CustomRenderModule>> custom;
        render::DefaultRenderScenario scenario(fixture.Gl.Window(), custom);
        glfwSetWindowSize(fixture.Gl.Window(), 19, 13);
        glfwPollEvents();
        i32 expectedWidth = 0, expectedHeight = 0;
        glfwGetFramebufferSize(fixture.Gl.Window(), &expectedWidth, &expectedHeight);
        i32 width = 0, height = 0;
        REQUIRE(scenario.RequestFrameCapture([&](const u8*, const i32 w, const i32 h) { width = w; height = h; }));
        glBindFramebuffer(GL_FRAMEBUFFER, 0);
        scenario.RenderWithoutCamera();
        scenario.Dispose();
        REQUIRE(width == expectedWidth);
        REQUIRE(height == expectedHeight);
    });
}
