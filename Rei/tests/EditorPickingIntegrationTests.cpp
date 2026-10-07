#include "pch.h"
#include "support/NativeRenderPipelineFixture.h"
#include "support/PickingTestSupport.h"
#include "support/GeometryTestSupport.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Modules/Editor/Components/EditorSelectionCollider.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectedTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Editor/EditorPointerInteractionState.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/Mesh/VertexObjects/CubeVertexObject.h"
#include "rei_behaviours/render/SpriteRenderer.h"

using namespace rei;
using namespace rei::tests;
using namespace rei::internal::engine;

namespace
{
    ecs::Entity CreatePickableMesh()
    {
        const auto entity = CreateEntity("owned picking mesh");
        GetEntityManager().AddBehaviour(entity, 7308, nlohmann::json(), false);
        auto registry = GetInternalWorld()->GetRegistry();
        registry->Get<Transform>(entity).GetLocalPosition() = {0, 0, 5};
        auto model = GetAssetManager().CreateAsset<render::Model>("picking cube", render::CubeVertexObject({}, {2, 2, 2}).GenerateMesh());
        registry->Get<render::MeshRenderer>(entity).SetModel(model);
        return entity;
    }

    void CheckRuntimeOwnership(const bool embedded)
    {
        NativeEngineFixture engine(PlayMode, "picking-runtime", PrepareRenderResources, embedded);
        engine.Start();
        engine.OnEngineThread([&]
        {
            const auto entity = CreatePickableMesh();
            auto registry = GetInternalWorld()->GetRegistry();
            CHECK_FALSE(registry->Has<editor::EditorSelectionCollider>(entity));
            CHECK(registry->Has<physics::PointerCollisionListener>(entity) == embedded);
            auto custom = std::make_shared<CountingPointerCollider>();
            registry->Get<physics::PointerCollisionListener>(entity).Collider = custom;
            auto& renderer = registry->Get<render::MeshRenderer>(entity);
            renderer.SetModel(renderer.GetModel());
            CHECK((registry->Get<physics::PointerCollisionListener>(entity).Collider == custom) == !embedded);
        });
        engine.Stop();
    }

    void CheckNoSelectionScopes(NativeEngineFixture& engine)
    {
        engine.OnEngineThread([] { GetProfiler().RequestCapture(8); });
        engine.WaitForNextFrames(10);
        const auto snapshot = GetProfiler().CopySnapshot(profiling::SnapshotView::LastCapture);
        REQUIRE(snapshot.State == profiling::CaptureState::Complete);
        REQUIRE(snapshot.FrameCount == 8);
        REQUIRE(snapshot.InvalidFrames == 0);
        bool foundScope = false;
        bool foundCounter = false;
        for (u32 i = 0; i < snapshot.MetricCount; ++i)
        {
            const auto& metric = snapshot.Metrics[i];
            if (metric.Id == profiling::markers::PICK_SELECTION.Id)
            {
                foundScope = true;
                CHECK(metric.Calls == 0);
            }
            if (metric.Id == profiling::markers::PICK_SELECTION_CANDIDATES.Id)
            {
                foundCounter = true;
                CHECK(metric.Value == 0);
            }
        }
        CHECK(foundScope);
        CHECK(foundCounter);
    }
}

TEST_CASE("Editor picking real engine renderer ownership survives replaced and unloaded models", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(EditorMode, "picking-ownership", PrepareRenderResources, true);
        engine.Start();
        engine.OnEngineThread([&]
        {
            const auto cameraEntity = CreateCamera();
            GetInternalWorld()->GetRegistry()->Get<render::Camera>(cameraEntity).SetOutputSize(32, 24);
            const auto entity = CreatePickableMesh();
            auto registry = GetInternalWorld()->GetRegistry();
            CHECK(registry->Has<editor::EditorSelectionCollider>(entity));
            CHECK_FALSE(registry->Has<physics::PointerCollisionListener>(entity));
            auto custom = std::make_shared<CountingPointerCollider>();
            auto& listener = registry->Get<physics::PointerCollisionListener>(entity);
            listener.Collider = custom;
            listener.IsInside = true;
            auto& renderer = registry->Get<render::MeshRenderer>(entity);
            const auto oldCollider = registry->Get<editor::EditorSelectionCollider>(entity).Collider;
            const auto replacement = GetAssetManager().CreateAsset<render::Model>("replacement outside ray", render::CubeVertexObject({20, 0, 0}, {2, 2, 2}).GenerateMesh());
            renderer.SetModel(replacement);
            CHECK(listener.Collider == custom);
            CHECK(listener.IsInside);
            CHECK(registry->Get<editor::EditorSelectionCollider>(entity).Collider != oldCollider);
            Refresh();
            math::Vector3 hit;
            const auto ray = registry->Get<render::Camera>(cameraEntity).GetScreenPointToRay(16, 12);
            const auto matrix = registry->Get<Transform>(entity).CalculateWorldModelMatrix();
            CHECK(oldCollider->Intersect(ray, matrix, hit));
            CHECK_FALSE(registry->Get<editor::EditorSelectionCollider>(entity).Collider->Intersect(ray, matrix, hit));
            renderer.SetModel(assets::AssetRef<render::Model>("unloaded-fixture-model"));
            CHECK(listener.Collider == custom);
            CHECK_FALSE(registry->Get<editor::EditorSelectionCollider>(entity).Collider->Intersect(ray, matrix, hit));
            const auto sprite = CreateEntity("owned sprite");
            auto spriteCustom = std::make_shared<CountingPointerCollider>();
            registry->Get<physics::PointerCollisionListener>(sprite).Collider = spriteCustom;
            GetEntityManager().AddBehaviour(sprite, 7309, nlohmann::json(), true);
            registry->Get<render::SpriteRenderer>(sprite).SetSprite({});
            CHECK(registry->Has<editor::EditorSelectionCollider>(sprite));
            CHECK(registry->Get<physics::PointerCollisionListener>(sprite).Collider == spriteCustom);
            const auto controls = GetInternalWorld()->GetFiltersRegistry()->Get<editor::SelectionByPointerBlockerTag>()->Entities();
            REQUIRE(controls.size() == 21);
            for (const auto control : controls)
            {
                CHECK(registry->Has<physics::PointerCollisionListener>(control));
                CHECK(registry->Get<physics::PointerCollisionListener>(control).Collider != nullptr);
                CHECK_FALSE(registry->Has<editor::EditorSelectionCollider>(control));
                CHECK_FALSE(registry->Has<editor::SelectableByPointerTag>(control));
            }
        });
        engine.Stop();
    });
}

TEST_CASE("Editor picking embedded Play retains legacy auto listener ownership", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([] { CheckRuntimeOwnership(true); });
}

TEST_CASE("Editor picking standalone Play preserves explicit listeners", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([] { CheckRuntimeOwnership(false); });
}

TEST_CASE("Editor picking real engine input runs one scene query per click and continuous hover", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(EditorMode, "picking-scheduler", PrepareRenderResources, true);
        engine.Start();
        ecs::Entity target = ecs::NULL_ENTITY;
        auto query = std::make_shared<CountingPointerCollider>();
        auto hover = std::make_shared<CountingPointerCollider>();
        engine.OnEngineThread([&]
        {
            CreateCamera();
            target = CreatePickableMesh();
            auto registry = GetInternalWorld()->GetRegistry();
            query->GeometryOverride = registry->Get<editor::EditorSelectionCollider>(target).Collider;
            registry->Get<editor::EditorSelectionCollider>(target).Collider = query;
            registry->Get<physics::PointerCollisionListener>(target).Collider = hover;
            Refresh();
        });
        engine.QueueMouse(WM_MOUSEMOVE, 16, 12);
        engine.WaitForNextFrames(4);
        CheckNoSelectionScopes(engine);
        engine.OnEngineThread([&]
        {
            CHECK(query->Calls == 0);
            CHECK(hover->Calls >= 4);
            CHECK(GetInternalWorld()->GetRegistry()->Get<physics::PointerCollisionListener>(target).IsInside);
        });
        engine.QueueMouse(WM_LBUTTONDOWN, 16, 12);
        engine.WaitForNextFrames(4);
        engine.OnEngineThread([&]
        {
            CHECK(query->Calls == 1);
            CHECK(editor::EditorPointerInteractionState::GetSelectionCandidate() == target);
            CHECK_FALSE(GetInternalWorld()->GetRegistry()->Has<editor::SelectedTag>(target));
            GetInternalWorld()->GetRegistry()->Get<Transform>(target).GetLocalPosition() = {20, 0, 5};
        });
        engine.QueueMouse(WM_MOUSEMOVE, 0, 0);
        engine.QueueMouse(WM_LBUTTONUP, 0, 0);
        engine.OnEngineThread([&]
        {
            CHECK(query->Calls == 1);
            CHECK(GetInternalWorld()->GetRegistry()->Has<editor::SelectedTag>(target));
            CHECK_FALSE(editor::EditorPointerInteractionState::HasSelectionCandidate());
            CHECK_FALSE(GetInternalWorld()->GetRegistry()->Get<physics::PointerCollisionListener>(target).IsInside);
        });
        ecs::Entity unavailable = ecs::NULL_ENTITY;
        engine.OnEngineThread([&] { unavailable = CreatePickableMesh(); Refresh(); });
        engine.QueueMouse(WM_MOUSEMOVE, 16, 12);
        engine.QueueMouse(WM_LBUTTONDOWN, 16, 12);
        engine.OnEngineThread([&]
        {
            REQUIRE(editor::EditorPointerInteractionState::GetSelectionCandidate() == unavailable);
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<render::MeshRenderer>(unavailable).SetModel(assets::AssetRef<render::Model>("unloaded-during-press"));
            CHECK_FALSE(registry->Get<editor::EditorSelectionCollider>(unavailable).Collider->IsAvailable());
        });
        engine.QueueMouse(WM_LBUTTONUP, 16, 12);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            CHECK(registry->Has<editor::SelectedTag>(target));
            CHECK_FALSE(registry->Has<editor::SelectedTag>(unavailable));
            CHECK_FALSE(editor::EditorPointerInteractionState::HasSelectionCandidate());
        });
        engine.Stop();
    });
}

TEST_CASE("Editor picking real engine FlyCamera hover and selection share updated camera", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(EditorMode, "picking-camera", PrepareRenderResources, true);
        engine.Start();
        ecs::Entity camera = ecs::NULL_ENTITY;
        auto query = std::make_shared<CountingPointerCollider>();
        auto hover = std::make_shared<CountingPointerCollider>();
        math::Ray continuousRayAtPress;
        f32 movementAtPress = 0;
        engine.OnEngineThread([&]
        {
            camera = CreateCamera();
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<render::Camera>(camera).SetPerspective(render::Perspective);
            const auto target = CreatePickableMesh();
            query->GeometryOverride = registry->Get<editor::EditorSelectionCollider>(target).Collider;
            registry->Get<editor::EditorSelectionCollider>(target).Collider = query;
            const auto blocker = CreateEntity("off-ray camera ordering blocker");
            registry->Get<Transform>(blocker).GetLocalPosition() = {20, 0, 5};
            registry->Get<editor::SelectionByPointerBlockerTag>(blocker);
            registry->Get<physics::PointerCollisionListener>(blocker).Collider = hover;
            query->ReadCameraPosition = [&, camera]
            {
                continuousRayAtPress = hover->LastRay;
                movementAtPress = 3.0f * static_cast<f32>(GetTime().GetDeltaSeconds());
                const auto position = GetInternalWorld()->GetRegistry()->Get<Transform>(camera).GetWorldPosition();
                const auto scanCode = MapVirtualKeyW('W', MAPVK_VK_TO_VSC);
                const auto keyUp = static_cast<LPARAM>((scanCode << 16) | 0xc0000001u);
                if (!PostMessageW(glfwGetWin32Window(glfwGetCurrentContext()), WM_KEYUP, 'W', keyUp)) throw std::runtime_error("Could not queue owned camera key release");
                return position;
            };
            Refresh();
        });
        engine.QueueMouse(WM_MOUSEMOVE, 16, 12);
        engine.OnEngineThread([]
        {
            const auto hwnd = glfwGetWin32Window(glfwGetCurrentContext());
            REQUIRE(PostMessageW(hwnd, WM_RBUTTONDOWN, MK_RBUTTON, MAKELPARAM(16, 12)) != 0);
            const auto keyDown = static_cast<LPARAM>((MapVirtualKeyW('W', MAPVK_VK_TO_VSC) << 16) | 1u);
            REQUIRE(PostMessageW(hwnd, WM_KEYDOWN, 'W', keyDown) != 0);
            REQUIRE(PostMessageW(hwnd, WM_LBUTTONDOWN, MK_LBUTTON | MK_RBUTTON, MAKELPARAM(16, 12)) != 0);
        });
        engine.WaitForNextFrames(3);
        engine.OnEngineThread([&]
        {
            REQUIRE(query->Calls == 1);
            // All observations belong to the press frame. With old ordering,
            // camera/hover/query would still be at z=0 before first Fly step.
            REQUIRE(movementAtPress > 0);
            CHECK(query->CameraPositionAtHit.z == Catch::Approx(movementAtPress).margin(0.0001f));
            CheckVector(query->LastRay.Origin, query->CameraPositionAtHit);
            CheckVector(continuousRayAtPress.Origin, query->CameraPositionAtHit);
            CHECK_FALSE(Input::IsKeyDown(GLFW_KEY_W));
        });
        engine.QueueMouse(WM_LBUTTONUP, 16, 12);
        engine.QueueMouse(WM_RBUTTONUP, 16, 12);
        engine.Stop();
    });
}

TEST_CASE("Editor picking real engine gizmo captured drag changes target without scene selection scopes", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(EditorMode, "picking-drag", PrepareRenderResources, true);
        engine.Start();
        ecs::Entity target = ecs::NULL_ENTITY;
        auto query = std::make_shared<CountingPointerCollider>();
        i32 arrowX = 0;
        engine.OnEngineThread([&]
        {
            CreateCamera();
            target = CreatePickableMesh();
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<editor::EditorSelectionCollider>(target).Collider = query;
            registry->Get<editor::SelectedTag>(target);
            Refresh();
        });
        engine.WaitForNextFrames(3);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            const auto entity = GetInternalWorld()->GetFiltersRegistry()->Get<editor::TransformationControl>()->First();
            const auto& control = registry->Get<editor::TransformationControl>(entity);
            const auto arrow = control.RightMovementArrow.Entity;
            REQUIRE(registry->Has<ActiveTag>(arrow));
            const auto& collider = registry->Get<physics::PointerCollisionListener>(arrow).Collider;
            const auto matrix = registry->Get<Transform>(arrow).CalculateWorldModelMatrix();
            const auto camera = render::Camera::GetMainCamera();
            for (i32 x = 20; x < 128; ++x)
            {
                math::Vector3 point;
                if (!collider->Intersect(camera.Get().GetScreenPointToRay(static_cast<f32>(x), 12), matrix, point)) continue;
                arrowX = x;
                break;
            }
            REQUIRE(arrowX > 0);
        });
        engine.QueueMouse(WM_MOUSEMOVE, arrowX, 12);
        engine.QueueMouse(WM_LBUTTONDOWN, arrowX, 12);
        engine.OnEngineThread([&]
        {
            const auto entity = GetInternalWorld()->GetFiltersRegistry()->Get<editor::TransformationControl>()->First();
            CHECK(GetInternalWorld()->GetRegistry()->Get<editor::TransformationControl>(entity).RightMovementArrow.DragActive);
            CHECK(editor::EditorPointerInteractionState::IsConsumed());
            CHECK(query->Calls == 0);
        });
        engine.QueueMouse(WM_MOUSEMOVE, arrowX + 6, 12);
        CheckNoSelectionScopes(engine);
        engine.OnEngineThread([&]
        {
            const auto position = GetInternalWorld()->GetRegistry()->Get<Transform>(target).GetWorldPosition();
            CHECK(position.x > 0.01f);
            CHECK(position.y == Catch::Approx(0).margin(0.001f));
            CHECK(position.z == Catch::Approx(5).margin(0.001f));
            CHECK(query->Calls == 0);
        });
        engine.QueueMouse(WM_LBUTTONUP, arrowX + 6, 12);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            const auto entity = GetInternalWorld()->GetFiltersRegistry()->Get<editor::TransformationControl>()->First();
            CHECK_FALSE(registry->Get<editor::TransformationControl>(entity).RightMovementArrow.DragActive);
            CHECK(registry->Has<editor::SelectedTag>(target));
            CHECK(query->Calls == 0);
        });
        engine.QueueMouse(WM_MOUSEMOVE, 0, 0);
        engine.QueueMouse(WM_LBUTTONDOWN, 0, 0);
        engine.OnEngineThread([&]
        {
            CHECK(query->Calls == 1);
            CHECK(editor::EditorPointerInteractionState::HasSelectionCandidate());
            CHECK_FALSE(editor::EditorPointerInteractionState::IsConsumed());
        });
        engine.QueueMouse(WM_MOUSEMOVE, arrowX + 6, 12);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            const auto entity = GetInternalWorld()->GetFiltersRegistry()->Get<editor::TransformationControl>()->First();
            CHECK(registry->Get<editor::TransformationControl>(entity).RightMovementArrow.DragActive);
            CHECK(editor::EditorPointerInteractionState::IsConsumed());
            CHECK(query->Calls == 1);
        });
        CheckNoSelectionScopes(engine);
        engine.QueueMouse(WM_LBUTTONUP, arrowX + 6, 12);
        engine.OnEngineThread([&] { CHECK(GetInternalWorld()->GetRegistry()->Has<editor::SelectedTag>(target)); });
        engine.Stop();
    });
}

TEST_CASE("Editor picking real engine UI priority and stale no camera state", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(EditorMode, "picking-ui", PrepareRenderResources, true);
        engine.Start();
        ecs::Entity image = ecs::NULL_ENTITY;
        ecs::Entity mesh = ecs::NULL_ENTITY;
        ecs::Entity camera = ecs::NULL_ENTITY;
        engine.OnEngineThread([&]
        {
            camera = CreateCamera();
            mesh = CreatePickableMesh();
            const auto canvas = CreateCanvas();
            image = CreateImage(canvas, render::Color::White(), {20, 16}, {});
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<Transform>(canvas).SetChildOrder(10);
            Refresh();
        });
        engine.QueueMouse(WM_MOUSEMOVE, 16, 12);
        engine.QueueMouse(WM_LBUTTONDOWN, 16, 12);
        engine.QueueMouse(WM_LBUTTONUP, 16, 12);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            CHECK(registry->Has<editor::SelectedTag>(image));
            CHECK_FALSE(registry->Has<editor::SelectedTag>(mesh));
            CHECK(registry->Get<physics::PointerCollisionListener>(image).IsInside);
            registry->Del<ActiveTag>(image);
        });
        engine.WaitForNextFrames(2);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            CHECK_FALSE(registry->Get<physics::PointerCollisionListener>(image).IsInside);
            registry->Get<ActiveTag>(image);
        });
        engine.WaitForNextFrames(2);
        engine.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            CHECK(registry->Get<physics::PointerCollisionListener>(image).IsInside);
            registry->Del<render::Camera>(camera);
        });
        engine.WaitForNextFrames(2);
        engine.OnEngineThread([&]
        {
            const auto& listener = GetInternalWorld()->GetRegistry()->Get<physics::PointerCollisionListener>(image);
            CHECK_FALSE(listener.IsInside);
            CHECK_FALSE(listener.DidEnter);
            CHECK_FALSE(listener.DidExit);
        });
        engine.Stop();
    });
}

TEST_CASE("Editor picking real input cycles overlapping models by origin depth despite pointer jitter", "[native][editor-picking][engine-integration][gl][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture engine(EditorMode, "picking-cycle", PrepareRenderResources, true);
        engine.Start();
        std::vector<ecs::Entity> targets;
        engine.OnEngineThread([&]
        {
            CreateCamera();
            auto registry = GetInternalWorld()->GetRegistry();
            for (const f32 depth : {5.0f, 7.0f, 9.0f})
            {
                const auto target = CreatePickableMesh();
                registry->Get<Transform>(target).GetLocalPosition() = {0, 0, depth};
                targets.push_back(target);
            }
            // Rotation tool leaves object body available; handle clicks remain blocked/consumed.
            const auto controls = GetInternalWorld()->GetFiltersRegistry()->Get<editor::TransformationControl>();
            for (const auto entity : controls->Entities()) registry->Get<editor::TransformationControl>(entity).Mode = editor::Rotation;
            Refresh();
        });
        const i32 xPositions[] = {17, 18, 17, 18};
        for (i32 click = 0; click < 4; ++click)
        {
            engine.QueueMouse(WM_MOUSEMOVE, xPositions[click], 11);
            engine.QueueMouse(WM_LBUTTONDOWN, xPositions[click], 11);
            engine.QueueMouse(WM_LBUTTONUP, xPositions[click], 11);
            engine.OnEngineThread([&]
            {
                const auto registry = GetInternalWorld()->GetRegistry();
                for (u32 index = 0; index < targets.size(); ++index)
                    CHECK(registry->Has<editor::SelectedTag>(targets[index]) == (index == click % targets.size()));
            });
        }
        engine.Stop();
    }, 20000, 1024);
}
