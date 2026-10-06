#include "pch.h"
#include "catch_amalgamated.hpp"
#include "support/NativeRenderPipelineFixture.h"
#include "Modules/Render/Modules/PostProcessingModule.h"
#include "rei_data_assets/render/RendererSettings.h"
#include <limits>

using namespace rei;
using namespace rei::tests;

namespace
{
    u8 DisplayCode(const f32 linear)
    {
        const f32 clipped = std::clamp(linear, 0.0f, 1.0f);
        const f32 srgb = clipped <= 0.0031308f ? 12.92f * clipped : 1.055f * std::pow(clipped, 1.0f / 2.4f) - 0.055f;
        return static_cast<u8>(std::lround(srgb * 255.0f));
    }

    std::shared_ptr<render::CameraModule> MakeHdrCamera(NativeRenderFixture& fixture)
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
}

TEST_CASE("HDR01 camera profiles serialize settings and sanitize invalid runtime values", "[native][hdr]")
{
    render::Camera camera;
    CHECK(camera.GetRendererSettings().Id == REI_DEFAULT_RENDERER_SETTINGS_ID);
    CHECK_FALSE(camera.GetRendererSettings().IsLoaded());
    CHECK(camera.GetRendererSettings().Get() == nullptr);
    render::RendererSettings settings;
    REQUIRE(settings.GetExposureEV() == 0);
    REQUIRE(settings.GetToneMapping() == render::Reinhard);
    settings.REI_SET(SerializedField("_exposureEV", 2.5));
    settings.REI_SET(SerializedField("_toneMapping", static_cast<i32>(render::Off)));
    CHECK(settings.GetExposureEV() == 2.5f);
    CHECK(settings.GetToneMapping() == render::Off);
    render::RendererSettings restored;
    const auto serialized = settings.REI_GET();
    restored.REI_SET(SerializedField("_exposureEV", serialized.at("_exposureEV")));
    restored.REI_SET(SerializedField("_toneMapping", serialized.at("_toneMapping")));
    CHECK(restored.GetExposureEV() == 2.5f);
    CHECK(restored.GetToneMapping() == render::Off);
    for (const f32 invalid : {std::numeric_limits<f32>::quiet_NaN(), std::numeric_limits<f32>::infinity(), -std::numeric_limits<f32>::infinity()})
    {
        settings.SetExposureEV(invalid);
        CHECK(settings.GetExposureEV() == 0);
    }
    settings.SetExposureEV(100);
    CHECK(settings.GetExposureEV() == 16);
    settings.SetExposureEV(-100);
    CHECK(settings.GetExposureEV() == -16);
    settings.SetToneMapping(static_cast<render::ToneMappingMode>(99));
    CHECK(settings.GetToneMapping() == render::Off);
}

TEST_CASE("HDR02 linear float framebuffer keeps highlights and alpha after resize", "[native][gl][hdr][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        render::FrameBuffer buffer(32, 32, render::FrameBufferFormat::LinearHdr);
        for (const i32 size : {32, 19})
        {
            buffer.EnableBuffer(size, size);
            glBindTexture(GL_TEXTURE_2D, buffer.GetColorTexture());
            i32 format = 0;
            glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_INTERNAL_FORMAT, &format);
            CHECK(format == GL_RGBA16F);
            glClearColor(2, 4, 8, 0.5f);
            glClear(GL_COLOR_BUFFER_BIT);
            std::array<f32, 4> pixel{};
            glReadPixels(1, 1, 1, 1, GL_RGBA, GL_FLOAT, pixel.data());
            CHECK((pixel == std::array<f32, 4>{2, 4, 8, 0.5f}));
        }
        CHECK(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("HDR03 final pass maps HDR once preserves alpha and reads live and replaced camera profiles", "[native][gl][hdr][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeHdrCamera(fixture);
        auto profile = fixture.Scene.Assets->CreateAsset<render::RendererSettings>();
        camera->GetCamera().Get().SetRendererSettings(profile);
        render::PostProcessingModule module(camera);
        module.Setup();
        render::FrameBuffer source(32, 32, render::FrameBufferFormat::LinearHdr);
        glClearColor(2, 4, 8, 0.5f);
        glClear(GL_COLOR_BUFFER_BIT);
        render::FrameBuffer target(32, 32);
        glViewport(0, 0, 32, 32);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_BLEND);
        const auto checkExposure = [&](const f32 ev)
        {
            profile->SetExposureEV(ev);
            target.EnableBuffer(32, 32);
            module.OnBeforeRender();
            module.Render(source);
            const auto scale = std::exp2(ev);
            RequirePixel(ReadPixel(), {DisplayCode(2 * scale / (1 + 2 * scale)), DisplayCode(4 * scale / (1 + 4 * scale)), DisplayCode(8 * scale / (1 + 8 * scale)), 128});
        };
        checkExposure(0);
        checkExposure(-2);
        checkExposure(1);
        profile->SetToneMapping(render::Off);
        profile->SetExposureEV(-2);
        module.OnBeforeRender();
        module.Render(source);
        RequirePixel(ReadPixel(), {DisplayCode(0.5f), 255, 255, 128});
        auto replacement = fixture.Scene.Assets->CreateAsset<render::RendererSettings>();
        replacement->SetExposureEV(-1);
        camera->GetCamera().Get().SetRendererSettings(replacement);
        module.OnBeforeRender();
        module.Render(source);
        RequirePixel(ReadPixel(), {DisplayCode(0.5f), DisplayCode(2.0f / 3), DisplayCode(0.8f), 128});
        source.EnableBuffer(32, 32);
        glClearColor(65504, 131008, 1e9f, 0.5f);
        glClear(GL_COLOR_BUFFER_BIT);
        target.EnableBuffer(32, 32);
        module.OnBeforeRender();
        module.Render(source);
        RequirePixel(ReadPixel(), {255, 255, 255, 128});
        camera->GetCamera().Get().SetRendererSettings({});
        module.OnBeforeRender();
        module.Render(source);
        RequirePixel(ReadPixel(), {255, 255, 255, 128});
        CHECK(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("HDR04 real engine tone maps scene highlights and leaves UI display colors unchanged", "[native][gl][engine-integration][renderer][hdr][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "hdr", PrepareRenderResources);
        engine.Start();
        assets::AssetRef<render::RendererSettings> settings;
        engine.OnEngineThread([&]
        {
            const auto camera = CreateCamera();
            settings = GetAssetManager().CreateAsset<render::RendererSettings>();
            GetInternalWorld()->GetRegistry()->Get<render::Camera>(camera).SetRendererSettings(settings);
            const auto mesh = CreateMesh(1, render::Color::White());
            auto material = GetAssetManager().CreateAsset<render::Material>(GetAssetManager().GetById<render::Shader>(REI_SHADER_LIGHT_SOURCE_ASSET_ID));
            material->SetColor("_Color", render::Color::White());
            material->SetFloat("_Strength", 8);
            GetInternalWorld()->GetRegistry()->Get<render::MeshRenderer>(mesh).SetMaterial(material);
            Refresh();
        });
        auto frame = Capture(engine);
        const auto expected = DisplayCode(8.0f / 9);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {expected, expected, expected, 255});
        engine.OnEngineThread([&] { settings->SetExposureEV(-1); });
        frame = Capture(engine);
        const auto darker = DisplayCode(0.8f);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {darker, darker, darker, 255});
        engine.OnEngineThread([]
        {
            CreateImage(CreateCanvas(), render::Color(128.0f / 255, 128.0f / 255, 128.0f / 255, 1), {32, 32}, {0, 0});
            Refresh();
        });
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {128, 128, 128, 255});
        engine.Stop();
    }, 30000, 1024);
}
namespace
{
    struct UniformWriteCounter
    {
        inline static UniformWriteCounter* Current = nullptr;
        PFNGLUNIFORM1FPROC FloatFunction = glad_glUniform1f;
        PFNGLUNIFORM1IPROC IntFunction = glad_glUniform1i;
        i32 FloatWrites = 0;
        i32 IntWrites = 0;

        UniformWriteCounter()
        {
            Current = this;
            glad_glUniform1f = +[](GLint location, GLfloat value)
            {
                Current->FloatWrites++;
                Current->FloatFunction(location, value);
            };
            glad_glUniform1i = +[](GLint location, GLint value)
            {
                Current->IntWrites++;
                Current->IntFunction(location, value);
            };
        }

        ~UniformWriteCounter()
        {
            glad_glUniform1f = FloatFunction;
            glad_glUniform1i = IntFunction;
            Current = nullptr;
        }
    };
}

TEST_CASE("HDR05 uniforms change only with effective settings and reset after shader recreation", "[native][gl][hdr][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        auto camera = MakeHdrCamera(fixture);
        auto profile = fixture.Scene.Assets->CreateAsset<render::RendererSettings>();
        camera->GetCamera().Get().SetRendererSettings(profile);
        render::PostProcessingModule module(camera);
        module.Setup();
        render::FrameBuffer source(32, 32, render::FrameBufferFormat::LinearHdr);
        source.EnableBuffer(32, 32);
        glClearColor(2, 4, 8, 1);
        glClear(GL_COLOR_BUFFER_BIT);
        render::FrameBuffer target(32, 32);
        target.EnableBuffer(32, 32);
        glViewport(0, 0, 32, 32);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_BLEND);
        UniformWriteCounter writes;
        const auto frame = [&] { module.OnBeforeRender(); module.Render(source); };
        frame();
        RequirePixel(ReadPixel(), {DisplayCode(2.0f / 3), DisplayCode(0.8f), DisplayCode(8.0f / 9), 255});
        REQUIRE(writes.FloatWrites == 1);
        REQUIRE(writes.IntWrites == 1);
        frame();
        frame();
        CHECK(writes.FloatWrites == 1);
        CHECK(writes.IntWrites == 1);
        profile->SetExposureEV(-1);
        frame();
        CHECK(writes.FloatWrites == 2);
        CHECK(writes.IntWrites == 1);
        profile->SetToneMapping(render::Off);
        frame();
        CHECK(writes.FloatWrites == 2);
        CHECK(writes.IntWrites == 2);
        auto same = fixture.Scene.Assets->CreateAsset<render::RendererSettings>();
        same->SetExposureEV(-1);
        same->SetToneMapping(render::Off);
        camera->GetCamera().Get().SetRendererSettings(same);
        frame();
        CHECK(writes.FloatWrites == 2);
        CHECK(writes.IntWrites == 2);
        camera->GetCamera().Get().SetRenderMode(Grayscale);
        frame();
        CHECK(writes.FloatWrites == 3);
        CHECK(writes.IntWrites == 3);
        camera->GetCamera().Get().SetRenderMode(Shaded);
        frame();
        CHECK(writes.FloatWrites == 3);
        CHECK(writes.IntWrites == 3);
        const auto material = fixture.Scene.Assets->GetById<render::Material>(REI_OVERLAY_TEXTURE_MATERIAL_ID);
        auto shader = material->GetShaderAsset();
        shader->Delete();
        shader->PostLoad();
        frame();
        CHECK(writes.FloatWrites == 4);
        CHECK(writes.IntWrites == 4);
        RequirePixel(ReadPixel(), {255, 255, 255, 255});
        camera->GetCamera().Get().SetRendererSettings({});
        frame();
        CHECK(writes.FloatWrites == 5);
        CHECK(writes.IntWrites == 4);
        camera->GetCamera().Get().SetRendererSettings(profile);
        profile.Record->State = assets::AssetState::Unloaded;
        frame();
        CHECK(writes.FloatWrites == 5);
        CHECK(writes.IntWrites == 4);
        profile.Record->State = assets::AssetState::Loaded;
        frame();
        CHECK(writes.FloatWrites == 6);
        CHECK(writes.IntWrites == 4);
        same->SetToneMapping(render::Reinhard);
        camera->GetCamera().Get().SetRendererSettings(same);
        camera->GetCamera().Get().SetRenderMode(Inversion);
        frame();
        RequirePixel(ReadPixel(), {DisplayCode(0.5f), DisplayCode(1.0f / 3), DisplayCode(0.2f), 255});
        CHECK(writes.FloatWrites == 7);
        CHECK(writes.IntWrites == 5);
        frame();
        CHECK(writes.FloatWrites == 7);
        CHECK(writes.IntWrites == 5);
        camera->GetCamera().Get().SetRenderMode(Shaded);
        frame();
        RequirePixel(ReadPixel(), {DisplayCode(0.5f), DisplayCode(2.0f / 3), DisplayCode(0.8f), 255});
        CHECK(writes.FloatWrites == 7);
        CHECK(writes.IntWrites == 6);
        camera->SetCamera({});
        frame();
        CHECK(writes.FloatWrites == 7);
        CHECK(writes.IntWrites == 6);
        CHECK(glGetError() == GL_NO_ERROR);
    });
}
