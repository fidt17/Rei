#include "pch.h"
#include "catch_amalgamated.hpp"
#include "support/NativeRenderPipelineFixture.h"
#include "Modules/Render/Lighting/LightUtility.h"
#include "Modules/Render/Lighting/LightSelector.h"
#include "Modules/Render/Lighting/LightGizmoGeometry.h"
#include "Modules/Render/Modules/Gizmos.h"
#include "Modules/Render/Modules/LightingRenderModule.h"
#include "Modules/Render/RenderScenario/FrameBuffer.h"
#include <limits>

using namespace rei;
using namespace rei::tests;

TEST_CASE("SPOT01 range and full cone angles sanitize setters and serialized values", "[native][spotlight][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, 7311, false);
        auto& light = fixture.Registry->Get<render::SpotLight>(entity);
        REQUIRE(light.GetInnerAngle() == 30);
        REQUIRE(light.GetOuterAngle() == 50);
        for (const auto invalid : {0.0f, -1.0f, std::numeric_limits<f32>::infinity(), std::numeric_limits<f32>::quiet_NaN()})
        {
            light.SetRange(invalid);
            REQUIRE(light.GetRange() == 0);
        }
        light.SetRange(0.001f);
        REQUIRE(light.GetRange() == 0.01f);
        light.REI_SET(SerializedField("_range", -5));
        REQUIRE(light.GetRange() == 0);
        light.SetOuterAngle(20);
        light.SetInnerAngle(100);
        REQUIRE(light.GetInnerAngle() == 20);
        light.SetOuterAngle(200);
        REQUIRE(light.GetOuterAngle() == 179);
        light.SetInnerAngle(-20);
        REQUIRE(light.GetInnerAngle() == 0);
        light.SetOuterAngle(std::numeric_limits<f32>::quiet_NaN());
        REQUIRE(light.GetOuterAngle() == 50);
        light.SetInnerAngle(std::numeric_limits<f32>::infinity());
        REQUIRE(light.GetInnerAngle() == 30);
        light.REI_SET(SerializedField("_innerAngle", 100));
        light.REI_SET(SerializedField("_outerAngle", 40));
        REQUIRE(light.GetInnerAngle() == 40);
        light.SetRange(7);
        light.SetInnerAngle(25);
        const auto json = light.REI_GET();
        render::SpotLight copy;
        for (const auto field : {"_range", "_innerAngle", "_outerAngle"}) copy.REI_SET(SerializedField(field, json.at(field)));
        REQUIRE(copy.GetRange() == 7);
        REQUIRE(copy.GetInnerAngle() == 25);
        REQUIRE(copy.GetOuterAngle() == 40);
        render::RendererSettings settings;
        settings.SetMaxSpotLights(-1);
        REQUIRE(settings.GetMaxSpotLights() == 0);
        settings.SetMaxSpotLights(99);
        REQUIRE(settings.GetMaxSpotLights() == REI_MAX_SPOT_LIGHTS_COUNT);
    });
}

TEST_CASE("SPOT02 world direction follows parent rotation and ignores nonuniform or zero scale", "[native][spotlight][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(1);
        const auto entity = fixture.Entity(2);
        fixture.Add(entity, 7311, false);
        auto& transform = fixture.Registry->Get<Transform>(entity);
        transform.SetParent(parent);
        auto& ancestor = fixture.Registry->Get<Transform>(parent);
        ancestor.SetRotation(math::Vector3(0, 90, 0));
        auto& light = fixture.Registry->Get<render::SpotLight>(entity);
        for (const auto scale : {math::Vector3(2, 3, 4), math::Vector3(0, 0, 0), math::Vector3(-2, 1, 4)})
        {
            ancestor.GetLocalScale() = scale;
            const auto direction = static_cast<glm::vec3>(light.GetWorldDirection());
            REQUIRE(glm::length(direction - glm::vec3(1, 0, 0)) < 0.00001f);
            REQUIRE(light.GetRange() == 10);
        }
        transform.SetRotation(math::Vector3(0, -90, 0));
        REQUIRE(glm::length(static_cast<glm::vec3>(light.GetWorldDirection()) - glm::vec3(0, 0, 1)) < 0.00001f);
    });
}

TEST_CASE("SPOT03 conservative cone culling retains boundary surfaces and rejects behind or beyond range", "[native][spotlight]")
{
    const auto point = [](const glm::vec3& center) { math::Bounds bounds; bounds.Include(center); return bounds; };
    const auto intersects = [&](const math::Bounds& bounds) { return render::light_utility::IntersectsSpot(bounds, {0, 0, 0}, {0, 0, 1}, 5, render::light_utility::ConeCosine(60)); };
    REQUIRE(intersects(point({0, 0, 4})));
    REQUIRE(intersects(point({2.5f, 0, std::sqrt(18.75f)})));
    REQUIRE_FALSE(intersects(point({0, 0, -1})));
    REQUIRE_FALSE(intersects(point({0, 0, 6})));
    REQUIRE_FALSE(intersects(point({3, 0, 1})));
    math::Bounds crossing;
    crossing.Include({-1, -1, 0});
    crossing.Include({1, 1, 2});
    REQUIRE(intersects(crossing));
    REQUIRE(intersects({}));
    REQUIRE_FALSE(render::light_utility::IntersectsSpot({}, {0, 0, 0}, {0, 0, 1}, 0, 0.9f));
}

TEST_CASE("SPOT04 gizmo contours match radial range and full angles without scaling", "[native][spotlight][light-gizmos]")
{
    const glm::vec3 origin(1, 2, 3);
    const auto point = render::light_gizmo_geometry::Point(origin, 5);
    REQUIRE(point.Outer.size() == 3 * render::light_gizmo_geometry::SEGMENTS * 2);
    for (const auto& vertex : point.Outer) REQUIRE(glm::length(vertex - origin) == Catch::Approx(5).margin(0.00001f));
    REQUIRE(render::light_gizmo_geometry::Point(origin, 0).Outer.empty());
    const auto rotation = glm::angleAxis(glm::radians(90.0f), glm::vec3(0, 1, 0));
    const auto spot = render::light_gizmo_geometry::Spot(origin, rotation, 5, 30, 60);
    REQUIRE_FALSE(spot.Inner.empty());
    for (const auto& vertex : spot.Outer) REQUIRE(glm::length(vertex - origin) <= 5.00001f);
    for (const auto& vertex : spot.Inner) REQUIRE(glm::length(vertex - origin) <= 5.00001f);
    const auto first = glm::inverse(rotation) * (spot.Outer[0] - origin);
    REQUIRE(first.x == Catch::Approx(2.5f).margin(0.00001f));
    REQUIRE(first.z == Catch::Approx(5 * std::cos(glm::radians(30.0f))).margin(0.00001f));
    REQUIRE(render::light_gizmo_geometry::Spot(origin, rotation, 0, 30, 60).Outer.empty());
}

TEST_CASE("SPOT_GL01 shader renders angular falloff hard edge range and additive point contribution", "[native][gl][spotlight][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        render::FrameBuffer target(32, 32, render::FrameBufferFormat::LinearHdr);
        target.EnableBuffer(32, 32);
        glDisable(GL_BLEND);
        glDisable(GL_DEPTH_TEST);
        const auto shader = fixture.Scene.Assets->GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        auto material = fixture.Scene.Assets->CreateAsset<render::Material>(shader);
        material->SetColor("_Color", render::Color(1, 1, 1, 0.4f));
        material->SetFloat("_Shininess", 100000);
        material->SetTexture("_MainTex", fixture.Scene.Assets->GetById<render::Texture>(REI_WHITE_FALLBACK_TEXTURE_ID));
        material->SetDepth(false);
        render::Mesh quad("spot oracle", {
            {{-1, -1, 0}, {0, 0, 1}, {0, 0}}, {{1, -1, 0}, {0, 0, 1}, {1, 0}},
            {{1, 1, 0}, {0, 0, 1}, {1, 1}}, {{-1, 1, 0}, {0, 0, 1}, {0, 1}}
        }, {0, 1, 2, 0, 2, 3}, {});
        quad.PostLoad();
        const auto pixel = [&](const f32 cosine, const f32 inner, const f32 outer, const f32 range = 1000, const i32 points = 0)
        {
            material->Use();
            shader->SetViewMatrices(glm::ortho(-1.0f, 1.0f, -1.0f, 1.0f, 0.1f, 10.0f), glm::mat4(1), glm::translate(glm::mat4(1), glm::vec3(0, 0, -2)));
            shader->SetInt("_PointLightsCount", points);
            shader->SetFloat("_AmbientLight.Strength", 0);
            shader->SetVector3("_PointLights[0].Position", {1.0f / 32, 1.0f / 32, -1});
            shader->SetFloat("_PointLights[0].Strength", 0.25f);
            shader->SetFloat("_PointLights[0].Range", 1000);
            shader->SetColor("_PointLights[0].Color", render::Color::White());
            shader->SetInt("_SpotLightsCount", 1);
            shader->SetVector3("_SpotLights[0].Position", {1.0f / 32, 1.0f / 32, -1});
            shader->SetVector3("_SpotLights[0].Direction", {std::sqrt(std::max(0.0f, 1 - cosine * cosine)), 0, -cosine});
            shader->SetFloat("_SpotLights[0].Strength", 0.25f);
            shader->SetFloat("_SpotLights[0].Range", range);
            shader->SetColor("_SpotLights[0].Color", render::Color::White());
            shader->SetFloat("_SpotLights[0].InnerCosine", inner);
            shader->SetFloat("_SpotLights[0].OuterCosine", outer);
            glClear(GL_COLOR_BUFFER_BIT);
            quad.Render();
            std::array<f32, 4> value{};
            glReadPixels(16, 16, 1, 1, GL_RGBA, GL_FLOAT, value.data());
            for (const auto channel : value) REQUIRE(std::isfinite(channel));
            REQUIRE(value[3] == Catch::Approx(0.4f).margin(0.001f));
            return value[0];
        };
        const auto inner = render::light_utility::ConeCosine(30);
        const auto outer = render::light_utility::ConeCosine(50);
        REQUIRE(pixel(1, inner, outer) == Catch::Approx(0.25f).margin(0.001f));
        REQUIRE(pixel((inner + outer) * 0.5f, inner, outer) == Catch::Approx(0.125f).margin(0.001f));
        REQUIRE(pixel(outer - 0.01f, inner, outer) == 0);
        REQUIRE(pixel(-1, inner, outer) == 0);
        REQUIRE(pixel(1, inner, outer, 1) == 0);
        REQUIRE(pixel(1, inner, outer, 0) == 0);
        REQUIRE(pixel(outer + 0.01f, outer, outer) == Catch::Approx(0.25f).margin(0.001f));
        REQUIRE(pixel(outer - 0.01f, outer, outer) == 0);
        REQUIRE(pixel(1, inner, outer, 1000, 1) == Catch::Approx(0.5f).margin(0.001f));
        quad.Dispose();
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("SPOT_GL02 snapshot selection responds to rotation range disable and independent budgets", "[native][gl][spotlight][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const auto entity = fixture.Scene.Entity(1);
        fixture.Scene.Add(entity, 7311, false);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        const auto cameraEntity = fixture.Scene.Entity(2);
        fixture.Scene.Add(cameraEntity, 7301, false);
        auto profile = fixture.Scene.Assets->CreateAsset<render::RendererSettings>();
        profile->SetMaxPointLights(0);
        profile->SetMaxSpotLights(1);
        fixture.Scene.Registry->Get<render::Camera>(cameraEntity).SetRendererSettings(profile);
        const ecs::ComponentRef<render::Camera> camera(fixture.Scene.Registry, cameraEntity);
        fixture.Scene.World->Refresh();
        render::LightSnapshot snapshot;
        render::LightSelector selector;
        math::Bounds bounds;
        bounds.Include({-0.1f, -0.1f, 2});
        bounds.Include({0.1f, 0.1f, 3});
        const auto count = [&]
        {
            selector.BeginFrame();
            snapshot.Update(camera);
            return selector.SelectSpots(bounds, glm::mat4(1), cameraEntity, snapshot).Count;
        };
        REQUIRE(count() == 1);
        REQUIRE(snapshot.GetPointLightLimit() == 0);
        auto& transform = fixture.Scene.Registry->Get<Transform>(entity);
        transform.SetRotation(math::Vector3(0, 180, 0));
        REQUIRE(count() == 0);
        transform.SetRotation(math::Vector3(0, 0, 0));
        REQUIRE(count() == 1);
        auto& light = fixture.Scene.Registry->Get<render::SpotLight>(entity);
        light.SetRange(1);
        REQUIRE(count() == 0);
        light.SetRange(10);
        light.Disable();
        REQUIRE(count() == 0);
        light.Enable();
        REQUIRE(count() == 1);
        profile->SetMaxSpotLights(0);
        REQUIRE(count() == 0);
        profile->SetMaxSpotLights(1);
        REQUIRE(count() == 1);
        fixture.Scene.Registry->Del<render::SpotLight>(entity);
        fixture.Scene.World->Refresh();
        REQUIRE(count() == 0);
    });
}

TEST_CASE("SPOT_PIPE01 real renderer reacts to spot direction disable range and camera movement", "[native][gl][engine-integration][spotlight][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "spotlight", PrepareRenderResources);
        engine.Start();
        ecs::Entity lightEntity = ecs::NULL_ENTITY;
        ecs::Entity cameraEntity = ecs::NULL_ENTITY;
        engine.OnEngineThread([&]
        {
            cameraEntity = CreateCamera();
            const auto mesh = CreateMesh(2, render::Color::White());
            auto material = GetAssetManager().CreateAsset<render::Material>(GetAssetManager().GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID));
            material->SetColor("_Color", render::Color::White());
            material->SetFloat("_Shininess", 0);
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<render::MeshRenderer>(mesh).SetMaterial(material);
            lightEntity = CreateEntity("pipeline spot");
            GetEntityManager().AddBehaviour(lightEntity, 7311, nlohmann::json(), false);
            registry->Get<render::SpotLight>(lightEntity).SetStrength(1);
            Refresh();
        });
        const auto center = [&](const std::shared_ptr<CapturedFrame>& frame) { return Pixel(*frame, frame->Width / 2, frame->Height / 2)[0]; };
        const auto lit = center(Capture(engine));
        REQUIRE(lit > 100);
        engine.OnEngineThread([&] { GetInternalWorld()->GetRegistry()->Get<Transform>(lightEntity).SetRotation(math::Vector3(0, 180, 0)); });
        REQUIRE(center(Capture(engine)) == 0);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<Transform>(lightEntity).SetRotation(math::Vector3(0, 0, 0));
            registry->Get<render::SpotLight>(lightEntity).Disable();
        });
        REQUIRE(center(Capture(engine)) == 0);
        engine.OnEngineThread([&]
        {
            auto& light = GetInternalWorld()->GetRegistry()->Get<render::SpotLight>(lightEntity);
            light.Enable();
            light.SetRange(1);
        });
        REQUIRE(center(Capture(engine)) == 0);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<render::SpotLight>(lightEntity).SetRange(10);
            registry->Get<Transform>(cameraEntity).GetLocalPosition() = {0.5f, 0, 0};
        });
        REQUIRE(center(Capture(engine)) > 30);
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("SPOT_GL03 selected light contours draw dim behind geometry and preserve depth state", "[native][gl][spotlight][light-gizmos][isolated]")
{
    IsolatedGl([]
    {
        NativeRenderFixture fixture;
        const auto cameraEntity = fixture.Scene.Entity(1);
        fixture.Scene.Add(cameraEntity, 7301, false);
        auto& camera = fixture.Scene.Registry->Get<render::Camera>(cameraEntity);
        camera.SetOutputSize(32, 32);
        camera.SetPerspective(render::Orthographic);
        auto cameraModule = std::make_shared<render::CameraModule>();
        cameraModule->SetCamera(ecs::ComponentRef<render::Camera>(fixture.Scene.Registry, cameraEntity));
        cameraModule->OnBeforeRender();
        auto gizmos = std::make_shared<render::Gizmos>(cameraModule);
        gizmos->Setup();
        Services::GetInstance()->SetGizmos(gizmos);
        const auto entity = fixture.Scene.Entity(2);
        fixture.Scene.Add(entity, 7306, false);
        fixture.Scene.Registry->Get<Transform>(entity).GetLocalPosition() = {0, 0, 2};
        fixture.Scene.Registry->Get<render::PointLight>(entity).SetRange(1);
        fixture.Scene.Registry->Get<ActiveTag>(entity);
        fixture.Scene.World->Refresh();
        render::FrameBuffer target(32, 32);
        target.EnableBuffer(32, 32);
        glViewport(0, 0, 32, 32);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glEnable(GL_DEPTH_TEST);
        glDepthFunc(GL_LESS);
        glDepthMask(GL_TRUE);
        glClearColor(0, 0, 0, 1);
        const auto energy = [&](const f64 depth)
        {
            glClearDepth(depth);
            glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            gizmos->Render(true);
            i32 depthFunction = 0;
            GLboolean depthWrite = GL_FALSE;
            glGetIntegerv(GL_DEPTH_FUNC, &depthFunction);
            glGetBooleanv(GL_DEPTH_WRITEMASK, &depthWrite);
            REQUIRE(depthFunction == GL_LESS);
            REQUIRE(depthWrite == GL_TRUE);
            REQUIRE(glIsEnabled(GL_DEPTH_TEST) == GL_TRUE);
            std::vector<u8> pixels(32 * 32 * 4);
            glReadPixels(0, 0, 32, 32, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
            u64 value = 0;
            for (u32 i = 0; i < pixels.size(); i += 4) value += pixels[i];
            return value;
        };
        REQUIRE(energy(1) == 0);
        fixture.Scene.Registry->Get<editor::SelectedTag>(entity);
        fixture.Scene.World->Refresh();
        const auto visible = energy(1);
        const auto hidden = energy(0);
        REQUIRE(visible > 0);
        REQUIRE(hidden > 0);
        REQUIRE(visible > hidden);
        GetEntityManager().GetBehaviourRegistry().DeleteBehaviour(entity, 7306);
        fixture.Scene.Add(entity, 7311, false);
        fixture.Scene.Registry->Get<render::SpotLight>(entity).SetRange(1);
        fixture.Scene.World->Refresh();
        REQUIRE(energy(1) > 0);
        fixture.Scene.Registry->Del<editor::SelectedTag>(entity);
        fixture.Scene.World->Refresh();
        REQUIRE(energy(1) == 0);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("SPOT_PIPE02 selected contours reach editor render pipeline and disappear on deselection", "[native][gl][engine-integration][spotlight][light-gizmos][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::EditorMode, "light contours", PrepareRenderResources, true);
        engine.Start();
        ecs::Entity light = ecs::NULL_ENTITY;
        engine.OnEngineThread([&]
        {
            GetEditorEventsRelay().GridRenderSettingsReceivedEvent(render::GridRenderSettings{false, false, false, 0});
            CreateCamera();
            light = CreatePointLight();
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<Transform>(light).GetLocalPosition() = {0, 0, 2};
            registry->Get<render::PointLight>(light).SetRange(0.5f);
            Refresh();
        });
        const auto energy = [&]
        {
            const auto frame = Capture(engine);
            u64 total = 0;
            for (u64 i = 0; i < frame->Pixels.size(); i += 4) total += frame->Pixels[i];
            return total;
        };
        REQUIRE(energy() == 0);
        engine.OnEngineThread([&]
        {
            GetInternalWorld()->GetRegistry()->Get<editor::SelectedTag>(light);
            Refresh();
        });
        REQUIRE(energy() > 0);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            GetEntityManager().DeleteBehaviour(light, 7306);
            GetEntityManager().AddBehaviour(light, 7311, nlohmann::json(), false);
            registry->Get<render::SpotLight>(light).SetRange(0.5f);
            Refresh();
        });
        REQUIRE(energy() > 0);
        engine.OnEngineThread([&]
        {
            GetInternalWorld()->GetRegistry()->Del<editor::SelectedTag>(light);
            Refresh();
        });
        REQUIRE(energy() == 0);
        engine.Stop();
    }, 30000, 1024);
}
