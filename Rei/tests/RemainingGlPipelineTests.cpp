#include "pch.h"
#include "catch_amalgamated.hpp"
#include "support/NativeRenderPipelineFixture.h"
#include "support/NativeImageTestSupport.h"
#include "support/RealGlCallTrace.h"
#include "Modules/Resources/Builders/TextureBuilder.h"
#include "Modules/Resources/Serialization/BinaryWriter.h"
#include "Modules/Render/RenderScenario/FrameBuffer.h"
#include "Modules/Render/Modules/UIRenderModule.h"
#include "rei_behaviours/ui/Text.h"

using namespace rei;
using namespace rei::tests;

TEST_CASE("PIPE12 shaded points shaded restores filled interior pixels", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        ecs::Entity cameraEntity = ecs::NULL_ENTITY;
        engine.OnEngineThread([&]
        {
            cameraEntity = CreateCamera();
            CreateMesh(1, render::Color(1, 0, 0, 1));
            Refresh();
        });
        auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2 - 3), {255, 0, 0, 255});
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetRenderMode(WireframePoints); });
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2 - 3), {0, 0, 0, 255});
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetRenderMode(Shaded); });
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2 - 3), {255, 0, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("GL33 imported PNG reaches actual image shader with source orientation and colors", "[native][coverage][coverage-remaining][gl][texture][importer][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        TemporaryDirectory files;
        // PNG stores top row first: red/green above blue/yellow.
        const std::vector<u8> sourcePixels = {
            255, 0, 0, 255, 0, 255, 0, 255,
            0, 0, 255, 255, 255, 255, 0, 255
        };
        const auto source = files.Write("orientation.png", PngBytes(4, sourcePixels));
        const auto targetFile = files.Write("orientation.texture", {});
        resources::BinaryWriter writer(targetFile.string(), 0);
        resources::TextureBuilder().BuildTextureAsset(source, writer);
        writer.Close();
        resources::BinaryReader reader(targetFile.string());
        auto texture = fixture.Scene.Assets->CreateAsset<render::Texture>(reader);
        REQUIRE(texture.IsLoaded());
        REQUIRE(texture->GetWidth() == 2);
        REQUIRE(texture->GetHeight() == 2);
        REQUIRE(glIsTexture(texture->GetId()) == GL_TRUE);
        texture->Use();
        // Point sampling makes each quadrant's oracle independent of interpolation.
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
        const auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_IMAGE_ASSET_ID);
        auto material = fixture.Scene.Assets->CreateAsset<render::Material>(shader);
        material->SetColor("_Color", render::Color::White());
        material->SetTexture("_MainTex", texture);
        material->SetDepth(false);
        render::Mesh mesh("imported texture quad", {
            {{-1, -1, 0}, {0, 0, 1}, {0, 0}}, {{1, -1, 0}, {0, 0, 1}, {1, 0}},
            {{1, 1, 0}, {0, 0, 1}, {1, 1}}, {{-1, 1, 0}, {0, 0, 1}, {0, 1}}
        }, {0, 1, 2, 0, 2, 3}, {});
        mesh.PostLoad();
        render::FrameBuffer target(32, 32);
        target.EnableBuffer(32, 32);
        glViewport(0, 0, 32, 32);
        glDisable(GL_BLEND);
        glClearColor(0, 0, 0, 1);
        glClear(GL_COLOR_BUFFER_BIT);
        material->Use();
        shader->SetViewMatrices(glm::mat4(1), glm::mat4(1), glm::mat4(1));
        mesh.Render();
        RequirePixel(ReadPixel(8, 24), {255, 0, 0, 255});
        RequirePixel(ReadPixel(24, 24), {0, 255, 0, 255});
        RequirePixel(ReadPixel(8, 8), {0, 0, 255, 255});
        RequirePixel(ReadPixel(24, 8), {255, 255, 0, 255});
        REQUIRE(glGetError() == GL_NO_ERROR);
        mesh.Dispose();
    });
}

TEST_CASE("GL34 text label batches six visible glyphs into one actual atlas draw", "[native][coverage][coverage-remaining][gl][ui-render][text][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const auto cameraEntity = CreateCamera();
        auto& camera = fixture.Scene.Registry->Get<render::Camera>(cameraEntity);
        camera.SetOutputSize(64, 64);
        auto cameraModule = std::make_shared<render::CameraModule>();
        cameraModule->SetCamera(ecs::ComponentRef<render::Camera>(fixture.Scene.Registry, cameraEntity));
        cameraModule->OnBeforeRender();
        const auto canvasEntity = CreateCanvas();
        const auto labelEntity = CreateEntity("batched label");
        GetEntityManager().AddBehaviour(labelEntity, 7305, nlohmann::json(), false);
        auto& rect = fixture.Scene.Registry->Get<ui::RectTransform>(labelEntity);
        rect.GetSizeDelta() = {64, 64};
        fixture.Scene.Registry->Get<Transform>(labelEntity).SetParent(canvasEntity);
        auto& text = fixture.Scene.Registry->Get<ui::Text>(labelEntity);
        const auto font = fixture.Scene.Assets->GetById<render::Font>("rei_roboto-regular.ttf");
        REQUIRE(font.IsLoaded());
        text.SetFont(font);
        text.SetColor(render::Color::White());
        text.SetAutoSize(false);
        text.SetSize(16);
        text.SetValue("ABC\nDEF");
        Refresh();
        render::UIRenderModule module(cameraModule);
        module.Setup();
        render::FrameBuffer target(64, 64);
        target.EnableBuffer(64, 64);
        glViewport(0, 0, 64, 64);
        glDisable(GL_SCISSOR_TEST);
        glDisable(GL_CULL_FACE);
        glClearColor(0, 0, 0, 1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        RealGlCallTrace trace;
        module.Render();
        REQUIRE(trace.ArrayDraws.size() == 1);
        CHECK(trace.ArrayDraws[0].Mode == GL_TRIANGLES);
        CHECK(trace.ArrayDraws[0].First == 0);
        CHECK(trace.ArrayDraws[0].Count == 36);
        CHECK(trace.ArrayDraws[0].Texture == font->GetAtlasTextureId());
        std::vector<u8> pixels(64 * 64 * 4);
        glReadPixels(0, 0, 64, 64, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
        u32 visiblePixels = 0;
        for (u32 index = 0; index < pixels.size(); index += 4) if (pixels[index] > 32) ++visiblePixels;
        CHECK(visiblePixels > 30);
        // Empty and space/newline-only values produce no actual GL draws.
        for (const std::string value : {"", " \n "})
        {
            CAPTURE(value);
            trace.ArrayDraws.clear();
            text.SetValue(value);
            module.Render();
            CHECK(trace.ArrayDraws.empty());
        }
        CHECK(glGetError() == GL_NO_ERROR);
    });
}
