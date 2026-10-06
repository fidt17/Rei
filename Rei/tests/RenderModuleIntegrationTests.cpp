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
            REQUIRE(ReadFloatUniform(shader, slot + ".Range") == 0);
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
        light.SetRange(7.5f);
        const auto verify = [&](const i32 count)
        {
            fixture.Scene.World->Refresh();
            lighting.OnBeforeRender();
            lighting.SetLightValues(*shader.Get());
            REQUIRE(ReadPointLightCount(*shader.Get()) == count);
            if (count == 0) RequireEmptyLightSlots(*shader.Get(), 0);
            else
            {
                REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Strength") == 0.75f);
                REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Range") == light.GetRange());
            }
        };
        verify(1);
        light.SetRange(3.25f);
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

TEST_CASE("Point light range persists with legacy default and rejects invalid distances", "[native][lighting][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, 7306, false);
        auto& light = fixture.Registry->Get<render::PointLight>(entity);
        CHECK(light.GetRange() == 10);
        light.REI_SET(SerializedField("_strength", 0.5f));
        CHECK(light.GetRange() == 10);
        light.SetRange(12.5f);
        const auto saved = light.REI_GET();
        CHECK(saved.at("_range") == 12.5f);
        light.SetRange(1);
        light.REI_SET(SerializedField("_range", saved.at("_range").get<f32>()));
        CHECK(light.GetRange() == 12.5f);
        for (const f32 range : {0.0f, -1.0f, std::numeric_limits<f32>::infinity(), std::numeric_limits<f32>::quiet_NaN()})
        {
            light.SetRange(range);
            CHECK(light.GetRange() == 0);
        }
        light.SetRange(0.001f);
        CHECK(light.GetRange() == 0.01f);
        light.REI_SET(SerializedField("_range", -2.0f));
        CHECK(light.GetRange() == 0);
    });
}

TEST_CASE("RENDER17 point light pixels attenuate smoothly and retain material alpha", "[native][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        render::FrameBuffer target(32, 32);
        target.EnableBuffer(32, 32);
        // Float target reads linear light directly, without sRGB encoding or LDR clipping.
        glBindTexture(GL_TEXTURE_2D, target.GetColorTexture());
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA32F, 32, 32, 0, GL_RGBA, GL_FLOAT, nullptr);
        REQUIRE(glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE);
        glViewport(0, 0, 32, 32);
        glDisable(GL_BLEND);
        glDisable(GL_DEPTH_TEST);
        const auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        auto material = fixture.Scene.Assets->CreateAsset<render::Material>(shader);
        material->SetColor("_Color", render::Color(1, 1, 1, 0.4f));
        material->SetFloat("_Shininess", 100000);
        material->SetTexture("_MainTex", fixture.Scene.Assets->GetById<render::Texture>(REI_WHITE_FALLBACK_TEXTURE_ID));
        material->SetDepth(false);
        render::Mesh quad("point light attenuation oracle", {
            {{-1, -1, 0}, {0, 0, 1}, {0, 0}}, {{1, -1, 0}, {0, 0, 1}, {1, 0}},
            {{1, 1, 0}, {0, 0, 1}, {1, 1}}, {{-1, 1, 0}, {0, 0, 1}, {0, 1}}
        }, {0, 1, 2, 0, 2, 3}, {});
        quad.PostLoad();
        const auto pixel = [&](const f32 distance, const f32 range)
        {
            material->Use();
            shader->SetViewMatrices(glm::ortho(-1.0f, 1.0f, -1.0f, 1.0f, 0.1f, 10.0f), glm::mat4(1), glm::translate(glm::mat4(1), glm::vec3(0, 0, -2)));
            shader->SetInt("_PointLightsCount", 1);
            shader->SetFloat("_AmbientLight.Strength", 0);
            shader->SetColor("_AmbientLight.Color", render::Color::White());
            // The center sample is at (1/32, 1/32, -2): constant incidence angle.
            shader->SetVector3("_PointLights[0].Position", {1.0f / 32, 1.0f / 32, -2 + distance});
            shader->SetFloat("_PointLights[0].Strength", 0.25f);
            shader->SetFloat("_PointLights[0].Range", range);
            shader->SetColor("_PointLights[0].Color", render::Color(1, 1, 1, 0));
            glClear(GL_COLOR_BUFFER_BIT);
            quad.Render();
            std::array<f32, 4> value{};
            glReadPixels(16, 16, 1, 1, GL_RGBA, GL_FLOAT, value.data());
            for (const auto channel : value) REQUIRE(std::isfinite(channel));
            REQUIRE(value[3] == Catch::Approx(0.4f).margin(1e-6f));
            return value[0];
        };
        const f32 oneUnit = pixel(1, 1000);
        REQUIRE(oneUnit == Catch::Approx(0.25f).margin(1e-5f));
        CHECK(pixel(2, 1000) / oneUnit == Catch::Approx(0.25f).margin(1e-5f));
        CHECK(pixel(4, 1000) / oneUnit == Catch::Approx(0.0625f).margin(1e-5f));
        CHECK(pixel(2, 4) == Catch::Approx(0.054931640625f).margin(1e-5f));
        CHECK(pixel(3.99f, 4) < 1e-5f);
        CHECK(pixel(4, 4) == 0);
        CHECK(pixel(5, 4) == 0);
        CHECK(pixel(1, 0) == 0);
        CHECK(pixel(1, -1) == 0);
        CHECK(pixel(0, 4) == 0);
        CHECK(pixel(0.00001f, 4) >= 0);
        quad.Dispose();
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("RENDER18 light source material stays visible without scene illumination", "[native][gl][lighting][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        render::FrameBuffer target(32, 32);
        target.EnableBuffer(32, 32);
        glViewport(0, 0, 32, 32);
        glDisable(GL_BLEND);
        const auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_LIGHT_SOURCE_ASSET_ID);
        auto material = fixture.Scene.Assets->CreateAsset<render::Material>(shader);
        material->SetColor("_Color", render::Color(1, 0.5f, 0, 1));
        material->SetFloat("_Strength", 1);
        material->SetDepth(false);
        render::Mesh quad("light source material oracle", {
            {{-1, -1, 0}, {0, 0, 1}, {0, 0}}, {{1, -1, 0}, {0, 0, 1}, {1, 0}},
            {{1, 1, 0}, {0, 0, 1}, {1, 1}}, {{-1, 1, 0}, {0, 0, 1}, {0, 1}}
        }, {0, 1, 2, 0, 2, 3}, {});
        quad.PostLoad();
        material->Use();
        shader->SetViewMatrices(glm::mat4(1), glm::mat4(1), glm::mat4(1));
        quad.Render();
        RequirePixel(ReadPixel(), {255, 128, 0, 255});
        quad.Dispose();
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}


TEST_CASE("LIGHT_SNAPSHOT01 snapshot survives repeated draws and updates parent transforms next frame", "[native][gl][lighting][uniform-batch][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        const auto parent = fixture.Scene.Entity(901);
        const auto entity = fixture.Scene.Entity(902);
        fixture.Scene.Add(entity, 7306, false);
        auto& light = fixture.Scene.Registry->Get<render::PointLight>(entity);
        light.SetStrength(3);
        light.SetRange(7);
        auto& transform = fixture.Scene.Registry->Get<Transform>(entity);
        transform.SetParent(parent);
        transform.GetLocalPosition() = {1, 2, 3};
        fixture.Scene.Registry->Get<Transform>(parent).GetLocalPosition() = {4, 5, 6};
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        fixture.Scene.World->Refresh();
        fixture.Scene.World->RefreshAll();
        render::LightingRenderModule module(camera);
        module.OnBeforeRender();
        module.SetLightValues(*shader.Get());
        light.SetStrength(8);
        fixture.Scene.Registry->Get<Transform>(parent).GetLocalPosition() = {10, 20, 30};
        module.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Strength") == 3);
        auto readPosition = [&]
        {
            shader->Use();
            i32 program = 0;
            glGetIntegerv(GL_CURRENT_PROGRAM, &program);
            std::array<f32, 3> value{};
            glGetUniformfv(program, shader->GetLocation("_PointLights[0].Position"), value.data());
            return value;
        };
        REQUIRE(readPosition() == std::array<f32, 3>{5, 7, 9});
        module.OnBeforeRender();
        module.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_PointLights[0].Strength") == 8);
        REQUIRE(readPosition() == std::array<f32, 3>{11, 22, 33});
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("LIGHT_SNAPSHOT02 snapshot restores native light values after material and raw GL writes", "[native][gl][lighting][uniform-batch][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeCamera(fixture);
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        render::LightingRenderModule module(camera);
        module.OnBeforeRender();
        module.SetLightValues(*shader.Get());
        render::Material overrideMaterial(shader);
        overrideMaterial.SetFloat("_AmbientLight.Strength", 9);
        overrideMaterial.Use();
        REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == 9);
        module.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == 0);
        shader->Use();
        glUniform1f(shader->GetLocation("_AmbientLight.Strength"), 7);
        REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == 7);
        module.SetLightValues(*shader.Get());
        REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == 0);
        shader->Delete();
        shader->PostLoad();
        module.SetLightValues(*shader.Get()); // Same frame, fresh program revision.
        REQUIRE(ReadFloatUniform(*shader.Get(), "_AmbientLight.Strength") == 0);
        RequireEmptyLightSlots(*shader.Get(), 0);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("SHADER_CONTEXT01 shader batch rebinds shared programs when GL context changes", "[native][gl][lighting][uniform-batch][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        render::Shader::UniformBatch batch(*shader.Get());
        shader->SetFloat("_Shininess", 3);
        auto* second = glfwCreateWindow(32, 32, "shared uniform test", nullptr, fixture.Gl.Window());
        REQUIRE(second != nullptr);
        const auto other = std::unique_ptr<GLFWwindow, decltype(&glfwDestroyWindow)>(second, glfwDestroyWindow);
        glfwMakeContextCurrent(second);
        glUseProgram(0);
        shader->SetFloat("_Shininess", 3);
        REQUIRE(glGetError() == GL_NO_ERROR);
        glUniform1f(shader->GetLocation("_Shininess"), 9);
        shader->SetFloat("_Shininess", 3);
        REQUIRE(ReadFloatUniform(*shader.Get(), "_Shininess") == 3);
        shader->SetFloat("_Shininess", 8);
        glfwMakeContextCurrent(fixture.Gl.Window());
        shader->SetFloat("_Shininess", 3);
        REQUIRE(ReadFloatUniform(*shader.Get(), "_Shininess") == 3);
        REQUIRE(glGetError() == GL_NO_ERROR);
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
        module.OnBeforeRender();
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
        module.OnBeforeRender();
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
        module.OnBeforeRender();
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
