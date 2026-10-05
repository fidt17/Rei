#include "pch.h"
#include "catch_amalgamated.hpp"
#include "support/NativeRenderPipelineFixture.h"
#include "rei_behaviours/render/light/AmbientLight.h"

using namespace rei;
using namespace rei::tests;

namespace
{
    struct LightingState
    {
        i32 PointCount = -1;
        std::array<f32, REI_MAX_POINT_LIGHTS_COUNT> PointStrengths{};
        f32 AmbientStrength = -1;
    };

    LightingState ReadLightingState(NativeEngineFixture& engine)
    {
        LightingState state;
        engine.OnEngineThread([&]
        {
            const auto shader = GetAssetManager().GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
            shader->Use();
            i32 program = 0;
            glGetIntegerv(GL_CURRENT_PROGRAM, &program);
            const auto location = [&](const std::string& name)
            {
                const auto value = shader->GetLocation(name);
                if (value < 0) throw std::runtime_error("Lighting uniform missing: " + name);
                return value;
            };
            glGetUniformiv(program, location("_PointLightsCount"), &state.PointCount);
            glGetUniformfv(program, location("_AmbientLight.Strength"), &state.AmbientStrength);
            for (i32 i = 0; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
                glGetUniformfv(program, location("_PointLights[" + std::to_string(i) + "].Strength"), &state.PointStrengths[i]);
            if (glGetError() != GL_NO_ERROR) throw std::runtime_error("Lighting readback encountered GL error");
        });
        return state;
    }

    void VerifyLightingFrame(NativeEngineFixture& engine, const i32 pointCount, const f32 ambientStrength = 0)
    {
        const auto frame = Capture(engine);
        // Same independent lighting equation as PIPE10: shininess 0, light (3,0,1), quad z=2.
        const auto brightness = ambientStrength + pointCount * 0.05f * (1.0f + 6.0f / std::sqrt(326.0f));
        const auto expected = static_cast<u8>(std::lround(255.0f * brightness));
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {expected, expected, expected, 255}, 2);
        const auto state = ReadLightingState(engine);
        REQUIRE(state.PointCount == pointCount);
        REQUIRE(state.AmbientStrength == ambientStrength);
        for (i32 i = 0; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
            REQUIRE(state.PointStrengths[i] == (i < pointCount ? 0.05f : 0));
        const auto repeated = Capture(engine);
        REQUIRE(repeated->Width == frame->Width);
        REQUIRE(repeated->Height == frame->Height);
        REQUIRE(repeated->Pixels == frame->Pixels);
    }

    ecs::Entity CreateAmbientLight(const f32 strength)
    {
        const auto entity = CreateEntity("pipeline ambient");
        GetEntityManager().AddBehaviour(entity, 7307, nlohmann::json(), false);
        GetInternalWorld()->GetRegistry()->Get<render::AmbientLight>(entity).REI_SET(SerializedField("_strength", strength));
        return entity;
    }
}

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

TEST_CASE("PIPE15 real engine point light lifecycle agrees with shader uniforms and pixels", "[native][gl][engine-integration][renderer][lighting][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "lighting-point", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([] { CreateLitMeshWithLights(0); });
        VerifyLightingFrame(engine, 0);
        std::vector<ecs::Entity> lights(5, ecs::NULL_ENTITY);
        for (i32 i = 0; i < 5; ++i)
        {
            engine.OnEngineThread([&] { lights[i] = CreatePointLight(); });
            VerifyLightingFrame(engine, std::min(i + 1, 4));
        }
        engine.OnEngineThread([&] { GetEntityManager().DeleteBehaviour(lights[4], 7306); });
        VerifyLightingFrame(engine, 4);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::PointLight>(lights[0]).Disable(); });
        VerifyLightingFrame(engine, 3);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::PointLight>(lights[0]).Enable(); });
        VerifyLightingFrame(engine, 4);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Del<ActiveTag>(lights[0]); });
        VerifyLightingFrame(engine, 3);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<ActiveTag>(lights[0]); });
        VerifyLightingFrame(engine, 4);
        engine.OnEngineThread([&]
        {
            for (i32 i = 0; i < 3; ++i) GetEntityManager().Destroy(lights[i]);
        });
        VerifyLightingFrame(engine, 1);
        engine.OnEngineThread([&] { GetEntityManager().Destroy(lights[3]); });
        VerifyLightingFrame(engine, 0);
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("PIPE16 real engine ambient lifecycle agrees with shader uniforms and pixels", "[native][gl][engine-integration][renderer][lighting][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "lighting-ambient", PrepareRenderResources);
        engine.Start();
        ecs::Entity first = ecs::NULL_ENTITY, second = ecs::NULL_ENTITY;
        engine.OnEngineThread([&]
        {
            CreateLitMeshWithLights(0);
            first = CreateAmbientLight(0.25f);
            second = CreateAmbientLight(0.1f);
        });
        VerifyLightingFrame(engine, 0, 0.25f);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::AmbientLight>(first).Disable(); });
        VerifyLightingFrame(engine, 0, 0.1f);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Del<ActiveTag>(second); });
        VerifyLightingFrame(engine, 0);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<render::AmbientLight>(first).Enable(); });
        VerifyLightingFrame(engine, 0, 0.25f);
        engine.OnEngineThread([&] { GetEntityManager().DeleteBehaviour(first, 7307); });
        VerifyLightingFrame(engine, 0);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<ActiveTag>(second); });
        VerifyLightingFrame(engine, 0, 0.1f);
        engine.OnEngineThread([&] { GetEntityManager().Destroy(second); });
        VerifyLightingFrame(engine, 0);
        engine.Stop();
    }, 30000, 1024);
}
