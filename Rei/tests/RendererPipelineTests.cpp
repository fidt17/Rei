#include "pch.h"
#include "catch_amalgamated.hpp"
#include "support/NativeRenderPipelineFixture.h"

using namespace rei;
using namespace rei::tests;

TEST_CASE("PIPE01 real engine shaded renderer depth-occludes far mesh", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([]
        {
            CreateCamera();
            CreateMesh(1, render::Color(1, 0, 0, 1));
            CreateMesh(2, render::Color(0, 1, 0, 1));
            Refresh();
        });
        const auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {255, 0, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE02 real engine MeshRenderer Disable reveals farther mesh", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        ecs::Entity nearEntity = ecs::NULL_ENTITY;
        engine.OnEngineThread([&]
        {
            CreateCamera();
            nearEntity = CreateMesh(1, render::Color(1, 0, 0, 1));
            CreateMesh(2, render::Color(0, 1, 0, 1));
            Refresh();
        });
        auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {255, 0, 0, 255});
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::MeshRenderer>(nearEntity).Disable(); });
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {0, 255, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE03 real engine inactive mesh entity contributes no pixels", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([]
        {
            CreateCamera();
            const auto red = CreateMesh(1, render::Color(1, 0, 0, 1));
            GetInternalWorld()->GetRegistry()->Del<ActiveTag>(red);
            Refresh();
        });
        const auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {0, 0, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE04 shaded wireframe shaded restores polygon fill and opaque pixel", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][isolated]")
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
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetRenderMode(WireframeLines); });
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2 - 3), {0, 0, 0, 255});
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetRenderMode(Shaded); });
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2 - 3), {255, 0, 0, 255});
        std::array<i32, 2> polygon{-1, -1};
        std::vector<u32> earlierRenderErrors;
        u32 queryError = GL_NO_ERROR;
        bool hasContext = false;
        engine.OnEngineThread([&]
        {
            hasContext = glfwGetCurrentContext() != nullptr;
            // Drain earlier rendering errors before querying unrelated GL state.
            // Keep them as a separate failing contract, not a glGetIntegerv error.
            for (u32 i = 0; i < 16; ++i)
            {
                const auto error = glGetError();
                if (error == GL_NO_ERROR) break;
                earlierRenderErrors.push_back(error);
            }
            glGetIntegerv(GL_POLYGON_MODE, polygon.data());
            queryError = glGetError();
        });
        CAPTURE(polygon[0], polygon[1], earlierRenderErrors, queryError, hasContext);
        REQUIRE(hasContext);
        CHECK(earlierRenderErrors.empty());
        CHECK(queryError == GL_NO_ERROR);
        // Core profile exposes one polygon mode shared by front/back faces.
        // Keep capacity for compatibility drivers, but do not require a second enum.
        CHECK(polygon[0] == GL_FILL);
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE05 builtin depth shader reciprocal-w pixels and material restoration", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][characterization][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        ecs::Entity cameraEntity = ecs::NULL_ENTITY, meshEntity = ecs::NULL_ENTITY;
        std::string materialId;
        engine.OnEngineThread([&]
        {
            cameraEntity = CreateCamera();
            meshEntity = CreateMesh(2, render::Color(1, 0, 0, 1));
            materialId = GetInternalWorld()->GetRegistry()->Get<render::MeshRenderer>(meshEntity).GetMaterial().Id;
            Refresh();
        });
        Capture(engine);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetRenderMode(Depth); });
        auto depth = Capture(engine);
        // Checked-in depth shader displays gl_FragCoord.w = reciprocal clip w.
        // Orthographic projection: w=1. Perspective at eye distance2: w=2.
        RequirePixel(Pixel(*depth, depth->Width / 2, depth->Height / 2), {255, 255, 255, 255});
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetPerspective(render::Perspective); });
        depth = Capture(engine);
        RequirePixel(Pixel(*depth, depth->Width / 2, depth->Height / 2), {128, 128, 128, 255});
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<Transform>(meshEntity).GetLocalPosition().z = 2; });
        depth = Capture(engine);
        RequirePixel(Pixel(*depth, depth->Width / 2, depth->Height / 2), {64, 64, 64, 255});
        engine.OnEngineThread([&]
        {
            if (GetInternalWorld()->GetRegistry()->Get<render::MeshRenderer>(meshEntity).GetMaterial().Id != materialId) throw std::runtime_error("Depth pass replaced original mesh material");
            GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetRenderMode(Shaded);
        });
        const auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {255, 0, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE06 real engine frame capture exports top-first red and green rows", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][capture][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([]
        {
            CreateCamera();
            const auto canvas = CreateCanvas();
            CreateImage(canvas, render::Color(1, 0, 0, 1), {32, 12}, {0, 6});
            CreateImage(canvas, render::Color(0, 1, 0, 1), {32, 12}, {0, -6});
            Refresh();
        });
        const auto frame = Capture(engine);
        REQUIRE(frame->Width == 32);
        REQUIRE(frame->Height == 24);
        RequirePixel(Pixel(*frame, 16, 3), {255, 0, 0, 255});
        RequirePixel(Pixel(*frame, 16, 20), {0, 255, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE07 real engine same material color writes reach independent capture pixels", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        ecs::Entity meshEntity = ecs::NULL_ENTITY;
        engine.OnEngineThread([&]
        {
            CreateCamera();
            meshEntity = CreateMesh(1, render::Color(1, 0, 0, 1));
            Refresh();
        });
        auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {255, 0, 0, 255});
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::MeshRenderer>(meshEntity).GetMaterial()->SetColor("_Color", render::Color(0, 0, 1, 1)); });
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {0, 0, 255, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE08 real engine destroying active camera clears frame to no-camera black", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][isolated]")
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
        Capture(engine);
        engine.OnEngineThread([&] { GetEntityManager().Destroy(cameraEntity); Refresh(); });
        const auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {0, 0, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE09 real engine lit shader with zero lights renders black", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][lighting][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([] { CreateLitMeshWithLights(0); Refresh(); });
        const auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {0, 0, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE10 real engine four point lights produce independently computed diffuse and specular pixel", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][lighting][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([] { CreateLitMeshWithLights(4); Refresh(); });
        const auto frame = Capture(engine);
        // Orthographic center pixel: world x/y = +/-1/6; light at (3,0,1), mesh z=2.
        // Four strengths .05; shininess 0 makes specular 1. Diffuse = 6/sqrt(326).
        const auto expected = static_cast<u8>(std::lround(255.0f * 4.0f * 0.05f * (1.0f + 6.0f / std::sqrt(326.0f))));
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {expected, expected, expected, 255}, 2);
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE11 real engine fifth point light stays outside four-slot shader cap", "[native][coverage][coverage-remaining][gl][engine-integration][renderer][lighting][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "renderer", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([] { CreateLitMeshWithLights(5); Refresh(); });
        const auto frame = Capture(engine);
        const auto expected = static_cast<u8>(std::lround(255.0f * 4.0f * 0.05f * (1.0f + 6.0f / std::sqrt(326.0f))));
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {expected, expected, expected, 255}, 2);
        engine.Stop();
    }, 30000, 1024);
}
