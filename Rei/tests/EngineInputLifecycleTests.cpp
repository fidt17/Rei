#include "pch.h"
#include "support/NativeRenderPipelineFixture.h"
#include "Modules/Input/Input.h"
#include "Modules/Assets/Types/TextAsset.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/ui/Button.h"
#include <atomic>

using namespace rei;
using namespace rei::tests;
using namespace rei::internal::engine;

namespace
{
    struct ButtonCounters
    {
        std::atomic<i32> Pressed = 0;
        std::atomic<i32> Released = 0;
        std::atomic<i32> Clicked = 0;
    };

    struct ButtonSnapshot
    {
        bool PointerInside = false;
        bool ListenerInside = false;
        bool Pressed = false;
        f32 MaterialRed = -1;
        render::Color ImageBaseColor;
    };

    void QueueMouse(NativeEngineFixture& engine, const UINT message, const i32 x, const i32 y)
    {
        engine.OnEngineThread([message, x, y]
        {
            const auto window = glfwGetCurrentContext();
            if (!window) throw std::runtime_error("Engine input prerequisite unavailable: current native context");
            // Deliver through glfwPollEvents after native Input::Update. A direct
            // callback at the engine task boundary would lose the polling edge.
            const auto hwnd = glfwGetWin32Window(window);
            const auto position = static_cast<LPARAM>((static_cast<u32>(y) << 16) | (static_cast<u32>(x) & 0xffff));
            const WPARAM flags = message == WM_LBUTTONDOWN ? MK_LBUTTON : 0;
            if (!PostMessageW(hwnd, message, flags, position)) throw std::runtime_error("Could not queue mouse event to owned hidden engine window");
        });
        engine.WaitForNextFrames(2);
    }

    ButtonSnapshot ReadButton(NativeEngineFixture& engine, const ecs::Entity entity)
    {
        ButtonSnapshot snapshot;
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            const auto& button = registry->Get<ui::Button>(entity);
            const auto& image = registry->Get<ui::Image>(entity);
            snapshot.ListenerInside = registry->Get<physics::PointerCollisionListener>(entity).IsInside;
            snapshot.PointerInside = button.IsPointerInside();
            snapshot.Pressed = button.IsPressed();
            snapshot.ImageBaseColor = image.GetColor();
            snapshot.MaterialRed = image.GetRenderMaterial().REI_GET().at("Properties").at("_Color").at("r").get<f32>();
        });
        return snapshot;
    }

    struct DestructionLease
    {
        std::shared_ptr<std::atomic<i32>> Destroyed;
        explicit DestructionLease(std::shared_ptr<std::atomic<i32>> destroyed) : Destroyed(std::move(destroyed)) {}
        ~DestructionLease() { ++*Destroyed; }
    };

    struct OwnedRuntimeComponent
    {
        std::shared_ptr<DestructionLease> Lease;
    };
}

TEST_CASE("UI03 real engine pointer hit drives Button material pixels and click lifecycle", "[native][coverage][coverage-remaining][engine-integration][gl][input][ui-button][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(PlayMode, "button-pipeline", PrepareRenderResources);
        engine.Start();
        ecs::Entity buttonEntity = ecs::NULL_ENTITY;
        auto counters = std::make_shared<ButtonCounters>();
        engine.OnEngineThread([&]
        {
            CreateCamera();
            const auto canvas = CreateCanvas();
            buttonEntity = CreateEntity("native button pipeline");
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<Transform>(buttonEntity).SetParent(canvas);
            GetEntityManager().AddBehaviour(buttonEntity, 7310, nlohmann::json(), true);
            auto& rect = registry->Get<ui::RectTransform>(buttonEntity);
            rect.GetSizeDelta() = {20, 16};
            auto& button = registry->Get<ui::Button>(buttonEntity);
            button.PressedEvent.append([counters] { ++counters->Pressed; });
            button.ReleasedEvent.append([counters] { ++counters->Released; });
            button.ClickedEvent.append([counters] { ++counters->Clicked; });
            const auto window = glfwGetCurrentContext();
            REQUIRE(window != nullptr);
            const auto mouse = glfwSetMouseButtonCallback(window, nullptr);
            const auto cursor = glfwSetCursorPosCallback(window, nullptr);
            REQUIRE(mouse != nullptr);
            REQUIRE(cursor != nullptr);
            glfwSetMouseButtonCallback(window, mouse);
            glfwSetCursorPosCallback(window, cursor);
            Refresh();
        });
        QueueMouse(engine, WM_MOUSEMOVE, 0, 0);
        auto snapshot = ReadButton(engine, buttonEntity);
        CHECK_FALSE(snapshot.PointerInside);
        CHECK_FALSE(snapshot.ListenerInside);
        CHECK(snapshot.MaterialRed == 1);
        auto frame = Capture(engine);
        RequirePixel(Pixel(*frame, 16, 12), {255, 255, 255, 255});

        QueueMouse(engine, WM_MOUSEMOVE, 16, 12);
        snapshot = ReadButton(engine, buttonEntity);
        CHECK(snapshot.ListenerInside);
        CHECK(snapshot.PointerInside);
        CHECK_FALSE(snapshot.Pressed);
        CHECK(snapshot.ImageBaseColor == render::Color::White());
        CHECK(snapshot.MaterialRed == Catch::Approx(0.9f).margin(1e-6f));
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, 16, 12), {230, 230, 230, 255});

        QueueMouse(engine, WM_LBUTTONDOWN, 16, 12);
        snapshot = ReadButton(engine, buttonEntity);
        CHECK(snapshot.Pressed);
        CHECK(snapshot.MaterialRed == 0.75f);
        CHECK(counters->Pressed == 1);
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, 16, 12), {191, 191, 191, 255});
        engine.WaitForNextFrames(2);
        CHECK(counters->Pressed == 1);

        QueueMouse(engine, WM_LBUTTONUP, 16, 12);
        snapshot = ReadButton(engine, buttonEntity);
        CHECK_FALSE(snapshot.Pressed);
        CHECK(snapshot.MaterialRed == Catch::Approx(0.9f).margin(1e-6f));
        CHECK(counters->Released == 1);
        CHECK(counters->Clicked == 1);
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, 16, 12), {230, 230, 230, 255});

        QueueMouse(engine, WM_LBUTTONDOWN, 16, 12);
        engine.OnEngineThread([buttonEntity] { GetInternalWorld()->GetRegistry()->Get<ui::Button>(buttonEntity).SetInteractable(false); });
        engine.WaitForNextFrames(2);
        snapshot = ReadButton(engine, buttonEntity);
        CHECK_FALSE(snapshot.Pressed);
        CHECK(snapshot.MaterialRed == 0.5f);
        frame = Capture(engine);
        RequirePixel(Pixel(*frame, 16, 12), {128, 128, 128, 255});
        QueueMouse(engine, WM_LBUTTONUP, 16, 12);
        CHECK(counters->Clicked == 1);
        CHECK(counters->Pressed == 2);
        engine.Stop();
    }, 40000, 1024);
}

TEST_CASE("LIFE02 native sessions dispose owned runtime objects before fixture cleanup and read fresh resources", "[native][coverage][coverage-remaining][engine-integration][lifecycle][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probes;
        auto componentsDestroyed = std::make_shared<std::atomic<i32>>(0);
        auto callbacksDestroyed = std::make_shared<std::atomic<i32>>(0);
        std::shared_ptr<ecs::EcsRegistry> oldRegistry;
        ecs::Entity oldEntity = ecs::NULL_ENTITY;
        assets::AssetRef<AssetProbe> retainedAsset;
        {
            NativeEngineFixture first(PlayMode, "RUNTIME_SESSION_A");
            first.Start();
            first.OnEngineThread([&]
            {
                oldRegistry = GetInternalWorld()->GetRegistry();
                oldEntity = GetEntityManager().CreateNewEntity("only session A");
                oldRegistry->Get<OwnedRuntimeComponent>(oldEntity).Lease = std::make_shared<DestructionLease>(componentsDestroyed);
                retainedAsset = GetAssetManager().CreateAssetWithId<AssetProbe>("session-A-owned", 51);
                auto lease = std::make_shared<DestructionLease>(callbacksDestroyed);
                first.Engine->ShutdownEvent.append([lease](i32) {});
                CHECK(GetAssetManager().GetById<assets::TextAsset>(REI_SHADER_INCLUDE_SHADER_COMMON_ASSET_ID)->GetValue() == "// RUNTIME_SESSION_A\n");
            });
            first.WaitForNextFrames(2);
            first.Stop();
            // Assertions deliberately precede NativeEngineFixture/BehaviourFixture
            // destruction: only native Shutdown has removed these payloads/entities.
            CHECK_FALSE(oldRegistry->IsAlive(oldEntity));
            CHECK(componentsDestroyed->load() == 1);
            CHECK(probes.State->Destroyed == 1);
            CHECK_FALSE(retainedAsset.IsLoaded());
            CHECK(GetAssetManager().GetLoadedAssetCount() == 0);
            CHECK(first.App->Shutdowns == 1);
            first.Engine.reset();
            CHECK(callbacksDestroyed->load() == 1);
        }
        NativeEngineFixture second(PlayMode, "RUNTIME_SESSION_B");
        second.Start();
        second.WaitForNextFrames(2);
        second.OnEngineThread([&]
        {
            CHECK(GetInternalWorld()->GetRegistry() != oldRegistry);
            CHECK_FALSE(oldRegistry->IsAlive(oldEntity));
            CHECK(GetAssetManager().InspectLoadedAsset("session-A-owned").at("status") == "unloaded");
            // Read actual new AssetManager payload, independently of ShaderGenerator's
            // known cached-include policy tested by the separate LIFE-02 shader case.
            CHECK(GetAssetManager().GetById<assets::TextAsset>(REI_SHADER_INCLUDE_SHADER_COMMON_ASSET_ID)->GetValue() == "// RUNTIME_SESSION_B\n");
            CHECK(GetInternalWorld()->GetFiltersRegistry()->Get<EntityInfo>()->Entities().empty());
        });
        CHECK(second.App->Starts == 1);
        second.Stop();
    }, 35000, 1536);
}

TEST_CASE("LIFE02 new native session resets input and replaces old relay callbacks", "[native][coverage][coverage-remaining][engine-integration][input][lifecycle][proposed-policy][isolated]")
{
    Isolated([]
    {
        auto oldEvents = std::make_shared<std::atomic<i32>>(0);
        auto newEvents = std::make_shared<std::atomic<i32>>(0);
        auto oldLeaseDestroyed = std::make_shared<std::atomic<i32>>(0);
        {
            NativeEngineFixture first;
            first.Start();
            first.OnEngineThread([&]
            {
                const auto window = glfwGetCurrentContext();
                REQUIRE(window != nullptr);
                auto lease = std::make_shared<DestructionLease>(oldLeaseDestroyed);
                GetEditorEventsRelay().EditorInputReceivedEvent.append([lease, oldEvents](const auto&) { ++*oldEvents; });
                const auto key = glfwSetKeyCallback(window, nullptr);
                const auto mouse = glfwSetMouseButtonCallback(window, nullptr);
                const auto cursor = glfwSetCursorPosCallback(window, nullptr);
                REQUIRE(key != nullptr);
                REQUIRE(mouse != nullptr);
                REQUIRE(cursor != nullptr);
                glfwSetKeyCallback(window, key);
                glfwSetMouseButtonCallback(window, mouse);
                glfwSetCursorPosCallback(window, cursor);
                cursor(window, 17, 9);
                key(window, GLFW_KEY_A, 0, GLFW_PRESS, 0);
                mouse(window, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
                CHECK(Input::IsKeyDown(GLFW_KEY_A));
                CHECK(Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT));
            });
            first.WaitForNextFrames(2);
            first.Stop();
        }
        const i32 priorEvents = oldEvents->load();
        REQUIRE(priorEvents >= 2);
        NativeEngineFixture second;
        second.Start();
        second.WaitForNextFrames(2);
        second.OnEngineThread([&]
        {
            CHECK_FALSE(Input::IsKeyDown(GLFW_KEY_A));
            CHECK_FALSE(Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT));
            // Proposed session policy: source replacement starts with fresh
            // callback coordinates; current Input::SetSource leaves old X/Y.
            CHECK(Input::GetMouseX() == 0);
            CHECK(Input::GetMouseY() == 0);
            const auto window = glfwGetCurrentContext();
            REQUIRE(window != nullptr);
            GetEditorEventsRelay().EditorInputReceivedEvent.append([newEvents](const auto&) { ++*newEvents; });
            const auto key = glfwSetKeyCallback(window, nullptr);
            REQUIRE(key != nullptr);
            glfwSetKeyCallback(window, key);
            key(window, GLFW_KEY_B, 0, GLFW_PRESS, 0);
            CHECK(Input::IsKeyDown(GLFW_KEY_B));
        });
        CHECK(oldEvents->load() == priorEvents);
        CHECK(newEvents->load() == 1);
        CHECK(oldLeaseDestroyed->load() == 1);
        second.Stop();
    }, 35000, 1536);
}

TEST_CASE("LIFE02 native Engine destruction releases runtime World before fixture cleanup", "[native][coverage][coverage-remaining][engine-integration][lifecycle][proposed-policy][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        std::weak_ptr<ecs::World> runtimeWorld;
        fixture.OnEngineThread([&] { runtimeWorld = GetInternalWorld(); });
        fixture.Stop();
        fixture.Engine.reset();
        // No test-owned strong pointer to runtime World. Test resource fixture
        // owns a different CPU World and its destructor has not run here.
        CHECK(runtimeWorld.expired());
    }, 25000, 1024);
}
