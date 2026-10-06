#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/InputTestFixture.h"
#include "support/PickingTestSupport.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Editor/Components/EditorSelectionCollider.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectedTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Editor/EditorPointerInteractionState.h"
#include "Modules/Editor/Systems/PointerEntitySelectionSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/TransformationControlDragUtilities.h"
#include "Modules/Input/Pointer/PointerCollisionSystem.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Components/ActiveTag.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/ui/RectTransform.h"

using namespace rei;
using namespace rei::tests;

namespace
{
    struct SelectionFixture
    {
        BehaviourFixture Native;
        InputFixture Input;
        ecs::Entity CameraEntity = Native.Entity(100);
        ecs::Entity Target = Native.Entity(101);
        std::shared_ptr<CountingPointerCollider> Collider = std::make_shared<CountingPointerCollider>();
        input::PointerCollisionSystem Hover{Native.World};
        editor::PointerEntitySelectionSystem Selection{Native.World};

        SelectionFixture()
        {
            auto& camera = Native.Registry->Get<render::Camera>(CameraEntity);
            camera = render::Camera(7301, CameraEntity);
            camera.SetOutputSize(256, 256);
            Native.Registry->Get<Transform>(Target).GetLocalPosition() = {0, 0, 5};
            Native.Registry->Get<ActiveTag>(Target);
            Native.Registry->Get<editor::SelectableByPointerTag>(Target);
            Native.Registry->Get<editor::EditorSelectionCollider>(Target).Collider = Collider;
            Native.World->RefreshAll();
            Input.Cursor(Input.First, 128, 128);
        }

        ~SelectionFixture()
        {
            editor::EditorPointerInteractionState::Reset();
            Native.Registry->Del<render::Camera>(CameraEntity);
        }

        void Frame() { Native.World->Refresh(); Native.World->RefreshAll(); Hover.OnUpdate(); Selection.OnUpdate(); }
        void Press() { rei::Input::Update(); Input.Mouse(Input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0); Frame(); }
        void Release() { rei::Input::Update(); Input.Mouse(Input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0); Frame(); }
        bool Selected(const ecs::Entity entity) { return Native.Registry->Has<editor::SelectedTag>(entity); }
    };
}

TEST_CASE("Editor picking idle skips scene geometry while explicit hover stays continuous", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        SelectionFixture f;
        auto hover = std::make_shared<CountingPointerCollider>();
        auto& listener = f.Native.Registry->Get<physics::PointerCollisionListener>(f.Target);
        listener.Collider = hover;
        for (i32 frame = 0; frame < 8; ++frame) f.Frame();
        CHECK(f.Collider->Calls == 0);
        CHECK(hover->Calls == 8);
        CHECK(listener.IsInside);
        CHECK_FALSE(listener.DidEnter);
        f.Press();
        CHECK(f.Collider->Calls == 1);
        CHECK_FALSE(f.Selected(f.Target));
        rei::Input::Update();
        for (i32 frame = 0; frame < 8; ++frame) f.Frame();
        CHECK(f.Collider->Calls == 1);
        f.Release();
        CHECK(f.Selected(f.Target));
        CHECK(f.Collider->Calls == 1);
        CHECK(hover->Calls == 18);
    });
}

TEST_CASE("Editor picking uses current object parent and camera transforms at press", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        SelectionFixture f;
        const auto parent = f.Native.Entity();
        f.Native.Registry->Get<Transform>(f.Target).SetParent(parent);
        f.Native.Registry->Get<Transform>(parent).GetLocalPosition() = {20, 0, 0};
        f.Frame();
        f.Native.Registry->Get<Transform>(parent).GetLocalPosition() = {3, 0, 0};
        f.Native.Registry->Get<Transform>(f.CameraEntity).GetLocalPosition() = {3, 0, 0};
        f.Press();
        CHECK(editor::EditorPointerInteractionState::GetSelectionCandidate() == f.Target);
        CheckVector(f.Collider->LastRay.Origin, {3, 0, 0});
        f.Native.Registry->Get<Transform>(parent).GetLocalPosition() = {20, 0, 0};
        f.Input.Cursor(f.Input.First, 0, 0);
        f.Release();
        CHECK(f.Selected(f.Target)); // Commit saved press candidate, without release re-query.
        CHECK(f.Collider->Calls == 1);
    });
}

TEST_CASE("Editor picking preserves filter first hit rather than nearest distance", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        SelectionFixture f;
        const auto other = f.Native.Entity();
        f.Native.Registry->Get<ActiveTag>(other);
        f.Native.Registry->Get<editor::SelectableByPointerTag>(other);
        f.Native.Registry->Get<editor::EditorSelectionCollider>(other).Collider = std::make_shared<physics::SphereCollider>();
        f.Native.World->RefreshAll();
        const auto order = f.Native.World->GetFiltersRegistry()->Get<editor::SelectableByPointerTag, ActiveTag>()->Entities();
        REQUIRE(order.size() == 2);
        f.Native.Registry->Get<Transform>(order.front()).GetLocalPosition() = {0, 0, 9};
        f.Native.Registry->Get<Transform>(order.back()).GetLocalPosition() = {0, 0, 3};
        f.Press();
        f.Release();
        CHECK(f.Selected(order.front()));
        CHECK_FALSE(f.Selected(order.back()));
    });
}

TEST_CASE("Editor picking modifiers are captured at press and empty click clears selection", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        SelectionFixture f;
        const auto previous = f.Native.Entity();
        for (const auto key : {GLFW_KEY_LEFT_CONTROL, GLFW_KEY_RIGHT_SHIFT})
        {
            f.Native.Registry->Get<editor::SelectedTag>(previous);
            f.Input.Key(f.Input.First, key, 0, GLFW_PRESS, 0);
            f.Press();
            f.Input.Key(f.Input.First, key, 0, GLFW_RELEASE, 0);
            f.Release();
            CHECK(f.Selected(previous));
            CHECK(f.Selected(f.Target));
            rei::Input::Update();
        }
        f.Input.Cursor(f.Input.First, 0, 0);
        f.Press();
        f.Release();
        CHECK_FALSE(f.Selected(previous));
        CHECK_FALSE(f.Selected(f.Target));
    });
}

TEST_CASE("Editor picking consumed drag preserves selection and does not repeat scene query", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        SelectionFixture f;
        const auto previous = f.Native.Entity();
        f.Native.Registry->Get<editor::SelectedTag>(previous);
        f.Press();
        editor::EditorPointerInteractionState::Consume();
        rei::Input::Update();
        for (i32 frame = 0; frame < 8; ++frame)
        {
            f.Native.Registry->Get<Transform>(f.Target).GetLocalPosition().x += 1;
            f.Frame();
        }
        f.Release();
        CHECK(f.Collider->Calls == 1);
        CHECK(f.Selected(previous));
        CHECK_FALSE(f.Selected(f.Target));
        CHECK_FALSE(editor::EditorPointerInteractionState::HasSelectionCandidate());
    });
}

TEST_CASE("Editor picking cancels invalid candidate without clearing previous selection", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        for (i32 removal = 0; removal < 6; ++removal)
        {
            SelectionFixture f;
            const auto previous = f.Native.Entity();
            f.Native.Registry->Get<editor::SelectedTag>(previous);
            f.Press();
            if (removal == 0) f.Native.Registry->Del<ActiveTag>(f.Target);
            if (removal == 1) f.Native.Registry->Del<editor::SelectableByPointerTag>(f.Target);
            if (removal == 2) f.Native.Registry->Del<editor::EditorSelectionCollider>(f.Target);
            if (removal == 3) f.Native.Registry->DestroyEntity(f.Target);
            if (removal == 4) f.Native.Registry->Del<Transform>(f.Target);
            if (removal == 5) f.Native.Registry->Get<editor::EditorSelectionCollider>(f.Target).Collider.reset();
            f.Release();
            CHECK(f.Selected(previous));
            CHECK_FALSE(editor::EditorPointerInteractionState::HasSelectionCandidate());
        }
    });
}

TEST_CASE("Editor picking cancels camera loss replacement input source and new world session", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        for (i32 reset = 0; reset < 4; ++reset)
        {
            SelectionFixture f;
            const auto previous = f.Native.Entity();
            f.Native.Registry->Get<editor::SelectedTag>(previous);
            f.Press();
            if (reset <= 1) f.Native.Registry->Del<render::Camera>(f.CameraEntity);
            if (reset == 1)
            {
                const auto next = f.Native.Entity();
                f.Native.Registry->Get<render::Camera>(next) = render::Camera(7301, next);
            }
            if (reset == 2) { f.Input.Select(f.Input.Second); f.Frame(); }
            if (reset == 3) { editor::PointerEntitySelectionSystem renewed(f.Native.World); }
            f.Release();
            CHECK(f.Selected(previous));
            CHECK_FALSE(f.Selected(f.Target));
            CHECK_FALSE(editor::EditorPointerInteractionState::HasSelectionCandidate());
        }
    });
}

TEST_CASE("Editor picking gizmo blocker bypasses scene query and held entry remains draggable", "[native][editor-picking][isolated]")
{
    Isolated([]
    {
        SelectionFixture f;
        const auto blocker = f.Native.Entity();
        f.Native.Registry->Get<ActiveTag>(blocker);
        f.Native.Registry->Get<editor::SelectionByPointerBlockerTag>(blocker);
        auto& listener = f.Native.Registry->Get<physics::PointerCollisionListener>(blocker);
        listener.Collider = std::make_shared<physics::SphereCollider>();
        f.Native.Registry->Get<Transform>(blocker).GetLocalPosition() = {0, 0, 5};
        f.Press();
        CHECK(listener.IsInside);
        CHECK(f.Collider->Calls == 0);
        CHECK_FALSE(editor::EditorPointerInteractionState::HasSelectionCandidate());
        rei::Input::Update();
        CHECK(editor::transformation_control_drag::ShouldStartPointerDrag(listener));
        listener.IsInside = false;
        listener.DidExit = true;
        CHECK_FALSE(editor::transformation_control_drag::ShouldStartPointerDrag(listener));
        f.Release();
        CHECK_FALSE(f.Selected(f.Target));
        rei::Input::Update();
        f.Input.Mouse(f.Input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        CHECK(editor::transformation_control_drag::ShouldStartPointerDrag(listener));
    });
}
