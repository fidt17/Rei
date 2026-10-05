#include "pch.h"
#include "support/NativeRenderPipelineFixture.h"
#include "Modules/Render/Camera/AssignMainCameraSystem.h"
#include "Modules/Render/Camera/MainCameraTag.h"
#include "Modules/Components/ActiveTag.h"
#include "rei_behaviours/render/camera/Camera.h"

using namespace rei;
using namespace rei::ecs;
using namespace rei::render;
using namespace rei::tests;

TEST_CASE("Runtime frames omit editor light-source markers", "[native][camera][gl][engine-integration][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(internal::engine::PlayMode, "runtime-markers", PrepareRenderResources);
        engine.Start();
        engine.OnEngineThread([]
        {
            CreateCamera();
            CreateMesh(2, Color::Green());
            const auto lightEntity = CreateEntity("runtime point light");
            GetInternalWorld()->GetRegistry()->Get<Transform>(lightEntity).GetLocalPosition() = {0, 0, 1};
            GetEntityManager().AddBehaviour(lightEntity, 7306, nlohmann::json(), false);
            auto& light = GetInternalWorld()->GetRegistry()->Get<PointLight>(lightEntity);
            light.SetColor(Color::Red());
            light.SetStrength(1);
            Refresh();
        });
        const auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, frame->Width / 2, frame->Height / 2), {0, 255, 0, 255});
        engine.Stop();
    }, 30000, 1024);
}

TEST_CASE("Camera selection uses existing component Enable and Disable", "[native][camera][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(101);
        const auto second = fixture.Entity(102);
        fixture.Registry->Get<ActiveTag>(first);
        fixture.Registry->Get<ActiveTag>(second);
        fixture.Registry->Get<Camera>(first) = Camera(8, first);
        fixture.Registry->Get<Camera>(second) = Camera(8, second);
        fixture.World->RefreshAll();
        const auto original = Camera::GetMainCamera();
        REQUIRE_FALSE(original.IsNull());
        const auto other = original.Get().GetEntity() == first ? second : first;
        original.Get().Disable();
        CHECK(Camera::GetMainCamera().Get().GetEntity() == other);
        fixture.Registry->Get<Camera>(other).Disable();
        CHECK(Camera::GetMainCamera().IsNull());
        fixture.Registry->Get<Camera>(other).Enable();
        CHECK(Camera::GetMainCamera().Get().GetEntity() == other);
        original.Get().Enable();
        CHECK(Camera::GetMainCamera().Get().GetEntity() == original.Get().GetEntity());
        fixture.Registry->Del<Camera>(first);
        fixture.Registry->Del<Camera>(second);
    });
}

TEST_CASE("Renderer follows active camera changes and clears selection", "[native][camera][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(101);
        const auto second = fixture.Entity(102);
        fixture.Registry->Get<ActiveTag>(first);
        fixture.Registry->Get<ActiveTag>(second);
        fixture.Registry->Get<Camera>(first) = Camera(8, first);
        fixture.Registry->Get<Camera>(second) = Camera(8, second);
        fixture.Registry->Get<Camera>(second).Disable();
        fixture.World->RefreshAll();
        auto renderer = std::make_shared<Renderer>();
        AssignMainCameraSystem system(fixture.World, renderer);
        system.OnUpdate();
        CHECK(renderer->GetCamera().Get().GetEntity() == first);
        CHECK(fixture.Registry->Has<MainCameraTag>(first));
        system.OnUpdate();
        fixture.Registry->Get<Camera>(first).Disable();
        fixture.Registry->Get<Camera>(second).Enable();
        system.OnUpdate();
        CHECK(renderer->GetCamera().Get().GetEntity() == second);
        CHECK_FALSE(fixture.Registry->Has<MainCameraTag>(first));
        CHECK(fixture.Registry->Has<MainCameraTag>(second));
        fixture.Registry->Get<Camera>(second).Disable();
        system.OnUpdate();
        CHECK(renderer->GetCamera().IsNull());
        CHECK_FALSE(fixture.Registry->Has<MainCameraTag>(second));
        fixture.Registry->Del<Camera>(first);
        fixture.Registry->Del<Camera>(second);
    });
}

TEST_CASE("Removed main camera releases tag and selects surviving camera", "[native][camera][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(101);
        const auto second = fixture.Entity(102);
        fixture.Registry->Get<ActiveTag>(first);
        fixture.Registry->Get<ActiveTag>(second);
        fixture.Registry->Get<Camera>(first) = Camera(8, first);
        fixture.Registry->Get<Camera>(second) = Camera(8, second);
        fixture.Registry->Get<Camera>(second).Disable();
        fixture.World->RefreshAll();
        auto renderer = std::make_shared<Renderer>();
        AssignMainCameraSystem system(fixture.World, renderer);
        system.OnUpdate();
        fixture.Registry->Del<Camera>(first);
        fixture.Registry->Get<Camera>(second).Enable();
        fixture.World->RefreshAll();
        system.OnUpdate();
        CHECK(renderer->GetCamera().Get().GetEntity() == second);
        CHECK_FALSE(fixture.Registry->Has<MainCameraTag>(first));
        fixture.Registry->Del<Camera>(second);
        fixture.World->RefreshAll();
        system.OnUpdate();
        CHECK(renderer->GetCamera().IsNull());
        CHECK_FALSE(fixture.Registry->Has<MainCameraTag>(second));
    });
}

TEST_CASE("Camera selection follows object ActiveTag without changing component enablement", "[native][camera][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(101);
        const auto second = fixture.Entity(102);
        fixture.Registry->Get<ActiveTag>(first);
        fixture.Registry->Get<ActiveTag>(second);
        fixture.Registry->Get<Camera>(first) = Camera(8, first);
        fixture.Registry->Get<Camera>(second) = Camera(8, second);
        fixture.World->RefreshAll();
        const auto original = Camera::GetMainCamera().Get().GetEntity();
        const auto other = original == first ? second : first;
        auto renderer = std::make_shared<Renderer>();
        AssignMainCameraSystem system(fixture.World, renderer);
        system.OnUpdate();
        fixture.Registry->Del<ActiveTag>(original);
        fixture.World->RefreshAll();
        system.OnUpdate();
        CHECK(renderer->GetCamera().Get().GetEntity() == other);
        CHECK_FALSE(fixture.Registry->Has<MainCameraTag>(original));
        CHECK(fixture.Registry->Get<Camera>(original).IsEnabled());
        fixture.Registry->Del<ActiveTag>(other);
        fixture.World->RefreshAll();
        system.OnUpdate();
        CHECK(Camera::GetMainCamera().IsNull());
        CHECK(renderer->GetCamera().IsNull());
        CHECK_FALSE(fixture.Registry->Has<MainCameraTag>(other));
        fixture.Registry->Get<ActiveTag>(original);
        fixture.World->RefreshAll();
        system.OnUpdate();
        CHECK(renderer->GetCamera().Get().GetEntity() == original);
        CHECK(fixture.Registry->Has<MainCameraTag>(original));
        fixture.Registry->Del<Camera>(first);
        fixture.Registry->Del<Camera>(second);
    });
}
