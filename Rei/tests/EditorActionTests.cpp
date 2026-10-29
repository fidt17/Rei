#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Editor/Actions/EditorActionStore.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include "Modules/Editor/Actions/RectTransformMoveAction.h"
#include "Modules/Editor/Actions/RectTransformResizeAction.h"
#include "Modules/Editor/Actions/RectTransformPivotAction.h"
#include "Modules/Editor/Actions/RectTransformAnchorsAction.h"
#include "Modules/Editor/Actions/EntityScaleAction.h"
#include "Modules/Editor/Actions/EntityRotateAction.h"
#include "Modules/Editor/Actions/EntityOrderAction.h"
#include "Modules/Editor/Actions/EntityReparentAction.h"
#include "Common/Transform/TransformHierarchyUtility.h"
#include "support/InputTestFixture.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/ResetTransformationControlsDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/TransformationControlDragUtilities.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Editor/EditorPointerInteractionState.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/HandleTransformationControlsMovementDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/HandleTransformationControlsRectTransformDragSystem.h"

using namespace rei;
using namespace rei::editor;
using namespace rei::tests;

namespace
{
    TransformationControlTargetState ReadState(const Transform& transform)
    {
        TransformationControlTargetState state;
        state.LocalPosition = transform.GetLocalPosition();
        state.LocalScale = transform.GetLocalScale();
        state.LocalRotation = transform.GetLocalRotation();
        return state;
    }

    void CheckState(const Transform& transform, const TransformationControlTargetState& state)
    {
        CheckVector(transform.GetLocalPosition(), state.LocalPosition);
        CheckVector(transform.GetLocalScale(), state.LocalScale);
        CheckRotation(transform.GetLocalRotation(), state.LocalRotation);
    }

    void ChangeValue(Transform& transform, const TransformationMode mode, const math::Vector3& value)
    {
        if (mode == Movement) transform.GetLocalPosition() = value;
        else if (mode == Scale) transform.GetLocalScale() = value;
        else transform.SetRotation(value);
    }

    struct TransformDragFixture
    {
        BehaviourFixture Native;
        InputFixture Input;
        std::shared_ptr<EditorActionStore> Actions = std::make_shared<EditorActionStore>();
        ecs::Entity Target = Native.Entity(1);
        ecs::Entity ControlEntity = Native.Registry->NewEntity();
        std::vector<u64> Recorded;
        ResetTransformationControlsDragSystem Finish{Native.World, Actions};

        TransformDragFixture()
        {
            Native.Registry->Get<Transform>(Target).GetLocalPosition() = {1, 2, 3};
            Control().PrimaryTargetEntity = Target;
            Control().TargetEntities = {Target};
            Native.World->RefreshAll();
            Actions->ActionRecorded.append([this](const u64 id) { Recorded.push_back(id); });
        }

        TransformationControl& Control() { return Native.Registry->Get<TransformationControl>(ControlEntity); }
        rei::Transform& TargetTransform() { return Native.Registry->Get<rei::Transform>(Target); }

        void Begin(const TransformationMode mode = Movement, const i32 handle = 0)
        {
            Input.Mouse(Input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
            auto& control = Control();
            control.Mode = mode;
            if (mode == Movement)
            {
                if (handle == 1) control.RightUpMovementPlane.DragActive = true;
                else control.RightMovementArrow.DragActive = true;
            }
            else if (mode == Scale)
            {
                TransformationControlScaleArrow* handles[] = {&control.RightScaleArrow, &control.UpScaleArrow, &control.ForwardScaleArrow, &control.RootScale};
                handles[handle]->DragActive = true;
            }
            else if (mode == Rotation)
            {
                TransformationControlRotationRing* handles[] = {&control.RightRotationRing, &control.UpRotationRing, &control.ForwardRotationRing};
                handles[handle]->DragActive = true;
            }
            transformation_control_drag::CaptureDragStartTargetStates(Native.World, control);
        }

        void Release()
        {
            Input.Mouse(Input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0);
            Finish.OnUpdate();
        }
    };
    struct RectMoveFixture : TransformDragFixture
    {
        ecs::Entity CanvasEntity = Native.Entity(10);
        ecs::Entity CameraEntity = Native.Entity(11);
        HandleTransformationControlsMovementDragSystem Movement{Native.World};
        HandleTransformationControlsRectTransformDragSystem Body{Native.World};

        RectMoveFixture()
        {
            EditorPointerInteractionState::Reset();
            Native.Registry->Get<ui::Canvas>(CanvasEntity) = ui::Canvas(7302, CanvasEntity);
            Native.Registry->Get<ui::Canvas>(CanvasEntity).REI_SET({{"_scaleMode", {{"Value", static_cast<i32>(ui::ConstantPixelSize)}}}});
            Native.Registry->Get<ui::RectTransform>(CanvasEntity) = ui::RectTransform(7303, CanvasEntity);
            Native.Registry->Get<render::Camera>(CameraEntity) = render::Camera(7301, CameraEntity);
            Native.Registry->Get<render::Camera>(CameraEntity).SetOutputSize(640, 480);
            Native.Registry->Get<ActiveTag>(CameraEntity);
            Native.Registry->Get<ui::RectTransform>(Target) = ui::RectTransform(7303, Target);
            Rect().GetAnchoredPosition() = {10, 20};
            Rect().GetSizeDelta() = {200, 160};
            TargetTransform().SetParent(CanvasEntity);
            auto& control = Control();
            control.RightMovementArrow.Entity = Part();
            control.UpMovementArrow.Entity = Part();
            control.RightUpMovementPlane.Entity = Part();
            control.RightMovementArrow.Direction = {1, 0, 0};
            control.UpMovementArrow.Direction = {0, 1, 0};
            for (auto* handle : {&control.TopLeftRectHandle, &control.TopRectHandle, &control.TopRightRectHandle, &control.LeftRectHandle,
                &control.RightRectHandle, &control.BottomLeftRectHandle, &control.BottomRectHandle, &control.BottomRightRectHandle}) handle->Entity = Part();
            const math::Vector2 directions[] = {{-1, 1}, {0, 1}, {1, 1}, {-1, 0}, {1, 0}, {-1, -1}, {0, -1}, {1, -1}};
            for (i32 i = 0; i < 8; i++) ResizeHandle(i).Direction = directions[i];
            Native.World->RefreshAll();
        }

        ~RectMoveFixture()
        {
            EditorPointerInteractionState::Reset();
            Native.Registry->Del<render::Camera>(CameraEntity);
        }

        ecs::Entity Part()
        {
            const auto entity = Native.Registry->NewEntity();
            Native.Registry->Get<physics::PointerCollisionListener>(entity);
            return entity;
        }

        ui::RectTransform& Rect() { return Native.Registry->Get<ui::RectTransform>(Target); }

        TransformationControlRectHandle& ResizeHandle(const i32 index)
        {
            auto& control = Control();
            TransformationControlRectHandle* handles[] = {&control.TopLeftRectHandle, &control.TopRectHandle, &control.TopRightRectHandle,
                &control.LeftRectHandle, &control.RightRectHandle, &control.BottomLeftRectHandle, &control.BottomRectHandle, &control.BottomRightRectHandle};
            return *handles[index];
        }

        ecs::Entity AddRectTarget(const i32 id, const math::Vector2& position)
        {
            const auto entity = Native.Entity(id);
            Native.Registry->Get<Transform>(entity).SetParent(CanvasEntity);
            auto& rect = Native.Registry->Get<ui::RectTransform>(entity);
            rect = ui::RectTransform(7303, entity);
            rect.GetAnchoredPosition() = position;
            rect.GetSizeDelta() = {100, 80};
            Control().TargetEntities.push_back(entity);
            Native.World->RefreshAll();
            return entity;
        }

        void PressResize(const i32 index)
        {
            Input.Cursor(Input.First, 330, 220);
            Control().Mode = editor::RectTransform;
            Native.Registry->Get<physics::PointerCollisionListener>(ResizeHandle(index).Entity).IsInside = true;
            Input.Mouse(Input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
            Body.OnUpdate();
        }

        void Press(const i32 kind)
        {
            Input.Cursor(Input.First, 330, 220);
            auto& control = Control();
            control.Mode = kind == 3 ? editor::RectTransform : editor::Movement;
            if (kind < 3)
            {
                const auto part = kind == 0 ? control.RightMovementArrow.Entity : kind == 1 ? control.UpMovementArrow.Entity : control.RightUpMovementPlane.Entity;
                Native.Registry->Get<physics::PointerCollisionListener>(part).IsInside = true;
            }
            Input.Mouse(Input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
            Update(kind);
        }

        void Update(const i32 kind)
        {
            if (kind == 3) Body.OnUpdate();
            else Movement.OnUpdate();
        }
    };
}

TEST_CASE("Editor action store records a completed move and restores its position", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        ecs::ComponentRef<Transform> transform(fixture.Registry, entity);
        const math::Vector3 before(1, 2, 3);
        const math::Vector3 after(4, 5, 6);
        transform.Get().GetLocalPosition() = after;

        EditorActionStore actions;
        std::vector<u64> recorded;
        actions.ActionRecorded.append([&](const u64 id) { recorded.push_back(id); });
        const auto id = actions.RecordApplied(std::make_unique<EntityMoveAction>(transform, before, after));
        REQUIRE(id != 0);
        REQUIRE(recorded.size() == 1);
        CHECK(recorded.front() == id);
        CheckVector(transform.Get().GetLocalPosition(), after);

        // Later changes to other properties must survive a position Undo/Redo.
        transform.Get().GetLocalScale() = {2, 3, 4};
        transform.Get().SetRotation(math::Vector3(10, 20, 30));
        const auto rotation = transform.Get().GetLocalRotation();
        REQUIRE(actions.Undo(id));
        CheckVector(transform.Get().GetLocalPosition(), before);
        CheckVector(transform.Get().GetLocalScale(), {2, 3, 4});
        CheckRotation(transform.Get().GetLocalRotation(), rotation);
        REQUIRE(actions.Redo(id));
        CheckVector(transform.Get().GetLocalPosition(), after);
        CheckVector(transform.Get().GetLocalScale(), {2, 3, 4});
        CheckRotation(transform.Get().GetLocalRotation(), rotation);
        CHECK(recorded.size() == 1);
    });
}

TEST_CASE("Editor action store restores scale without changing position or rotation", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        ecs::ComponentRef<Transform> transform(fixture.Registry, fixture.Entity());
        const math::Vector3 before(1, 2, -3);
        const math::Vector3 after(0, 4, 5);
        transform.Get().GetLocalScale() = after;
        EditorActionStore actions;
        const auto id = actions.RecordApplied(std::make_unique<EntityScaleAction>(transform, before, after));
        transform.Get().GetLocalPosition() = {7, 8, 9};
        transform.Get().SetRotation(math::Vector3(10, 20, 30));
        const auto rotation = transform.Get().GetLocalRotation();
        REQUIRE(actions.Undo(id));
        CheckVector(transform.Get().GetLocalScale(), before);
        CheckVector(transform.Get().GetLocalPosition(), {7, 8, 9});
        CheckRotation(transform.Get().GetLocalRotation(), rotation);
        REQUIRE(actions.Redo(id));
        CheckVector(transform.Get().GetLocalScale(), after);
        CheckVector(transform.Get().GetLocalPosition(), {7, 8, 9});
        CheckRotation(transform.Get().GetLocalRotation(), rotation);
    });
}

TEST_CASE("Editor action store restores rotation without changing position or scale", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        ecs::ComponentRef<Transform> transform(fixture.Registry, fixture.Entity());
        transform.Get().SetRotation(math::Vector3(10, 20, 30));
        const auto before = transform.Get().GetLocalRotation();
        transform.Get().SetRotation(math::Vector3(-45, 60, 90));
        const auto after = transform.Get().GetLocalRotation();
        EditorActionStore actions;
        const auto id = actions.RecordApplied(std::make_unique<EntityRotateAction>(transform, before, after));
        transform.Get().GetLocalPosition() = {7, 8, 9};
        transform.Get().GetLocalScale() = {2, 3, 4};
        REQUIRE(actions.Undo(id));
        CheckRotation(transform.Get().GetLocalRotation(), before);
        CheckVector(transform.Get().GetLocalPosition(), {7, 8, 9});
        CheckVector(transform.Get().GetLocalScale(), {2, 3, 4});
        REQUIRE(actions.Redo(id));
        CheckRotation(transform.Get().GetLocalRotation(), after);
        CheckVector(transform.Get().GetLocalPosition(), {7, 8, 9});
        CheckVector(transform.Get().GetLocalScale(), {2, 3, 4});
    });
}

TEST_CASE("Editor action store addresses independent edits by ID", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        ecs::ComponentRef<Transform> first(fixture.Registry, fixture.Entity(1));
        ecs::ComponentRef<Transform> second(fixture.Registry, fixture.Entity(2));
        first.Get().GetLocalPosition() = {1, 0, 0};
        second.Get().GetLocalPosition() = {2, 0, 0};
        EditorActionStore actions;
        const auto firstId = actions.RecordApplied(std::make_unique<EntityMoveAction>(first, math::Vector3{}, math::Vector3(1, 0, 0)));
        const auto secondId = actions.RecordApplied(std::make_unique<EntityMoveAction>(second, math::Vector3{}, math::Vector3(2, 0, 0)));
        REQUIRE(firstId != secondId);

        REQUIRE(actions.Undo(firstId));
        CheckVector(first.Get().GetLocalPosition(), {});
        CheckVector(second.Get().GetLocalPosition(), {2, 0, 0});
        REQUIRE(actions.Redo(firstId));
        REQUIRE(actions.Undo(secondId));
        CheckVector(first.Get().GetLocalPosition(), {1, 0, 0});
        CheckVector(second.Get().GetLocalPosition(), {});
    });
}

TEST_CASE("Editor action store rejects missing actions and propagates failed replay", "[native][editor-actions]")
{
    EditorActionStore actions;
    CHECK(actions.RecordApplied(nullptr) == 0);
    CHECK_FALSE(actions.Undo(0));
    CHECK_FALSE(actions.Redo(123));

    const auto id = actions.RecordApplied(std::make_unique<EntityMoveAction>(ecs::ComponentRef<Transform>{}, math::Vector3{}, math::Vector3(1, 2, 3)));
    REQUIRE(id != 0);
    CHECK_FALSE(actions.Undo(id));
    CHECK_FALSE(actions.Redo(id));
    const auto scaleId = actions.RecordApplied(std::make_unique<EntityScaleAction>(ecs::ComponentRef<Transform>{}, math::Vector3(1, 1, 1), math::Vector3(2, 3, 4)));
    CHECK_FALSE(actions.Undo(scaleId));
    CHECK_FALSE(actions.Redo(scaleId));
    const auto rotateId = actions.RecordApplied(std::make_unique<EntityRotateAction>(ecs::ComponentRef<Transform>{}, glm::quat(1, 0, 0, 0), glm::quat(0, 0, 0, 1)));
    CHECK_FALSE(actions.Undo(rotateId));
    CHECK_FALSE(actions.Redo(rotateId));
}

TEST_CASE("Editor action store records gizmo Move once on release and replays it", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const bool plane : {false, true})
        {
            CAPTURE(plane);
            TransformDragFixture fixture;
            fixture.Begin(Movement, plane ? 1 : 0);
            fixture.TargetTransform().GetLocalPosition() = {4, 5, 6};
            fixture.Finish.OnUpdate();
            fixture.Finish.OnUpdate();
            REQUIRE(fixture.Recorded.empty());
            fixture.Release();
            REQUIRE(fixture.Recorded.size() == 1);
            const auto id = fixture.Recorded.front();
            CheckVector(fixture.TargetTransform().GetLocalPosition(), {4, 5, 6});
            CHECK(fixture.Control().DragStartTargetStates.empty());
            fixture.Finish.OnUpdate();
            REQUIRE(fixture.Recorded.size() == 1);
            REQUIRE(fixture.Actions->Undo(id));
            CheckVector(fixture.TargetTransform().GetLocalPosition(), {1, 2, 3});
            REQUIRE(fixture.Actions->Redo(id));
            CheckVector(fixture.TargetTransform().GetLocalPosition(), {4, 5, 6});
            CHECK(fixture.Recorded.size() == 1);
        }
    });
}

TEST_CASE("Editor action store skips unchanged and unsupported gizmo drags", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const std::string reason : {"unchanged", "returned", "scale", "rotation", "rect", "inactive"})
        {
            CAPTURE(reason);
            TransformDragFixture fixture;
            fixture.Begin();
            if (reason != "unchanged") fixture.TargetTransform().GetLocalPosition() = {4, 5, 6};
            if (reason == "returned") fixture.TargetTransform().GetLocalPosition() = {1, 2, 3};
            if (reason == "scale") fixture.Control().Mode = Scale;
            if (reason == "rotation") fixture.Control().Mode = Rotation;
            if (reason == "rect") fixture.Native.Registry->Get<ui::RectTransform>(fixture.Target);
            if (reason == "inactive") fixture.Control().RightMovementArrow.DragActive = false;
            fixture.Release();
            fixture.Finish.OnUpdate();
            CHECK(fixture.Recorded.empty());
            CHECK(fixture.Control().DragStartTargetStates.empty());
        }
    });
}

TEST_CASE("Editor action store records Scale and Rotation handles once on release", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const auto mode : {Scale, Rotation})
        {
            for (i32 handle = 0; handle < (mode == Scale ? 4 : 3); ++handle)
            {
                CAPTURE(mode, handle);
                TransformDragFixture fixture;
                fixture.Begin(mode, handle);
                const auto before = fixture.Control().DragStartTargetStates.front();
                if (mode == Scale) fixture.TargetTransform().GetLocalScale() = {-2, 0, 4};
                else fixture.TargetTransform().SetRotation(math::Vector3(10, 20, 30));
                const auto afterScale = fixture.TargetTransform().GetLocalScale();
                const auto afterRotation = fixture.TargetTransform().GetLocalRotation();
                fixture.Finish.OnUpdate();
                fixture.Finish.OnUpdate();
                REQUIRE(fixture.Recorded.empty());
                fixture.Release();
                REQUIRE(fixture.Recorded.size() == 1);
                const auto id = fixture.Recorded.front();
                CHECK(fixture.Control().DragStartTargetStates.empty());
                fixture.Finish.OnUpdate();
                REQUIRE(fixture.Recorded.size() == 1);
                REQUIRE(fixture.Actions->Undo(id));
                CheckVector(fixture.TargetTransform().GetLocalPosition(), before.LocalPosition);
                CheckVector(fixture.TargetTransform().GetLocalScale(), before.LocalScale);
                CheckRotation(fixture.TargetTransform().GetLocalRotation(), before.LocalRotation);
                REQUIRE(fixture.Actions->Redo(id));
                CheckVector(fixture.TargetTransform().GetLocalPosition(), before.LocalPosition);
                CheckVector(fixture.TargetTransform().GetLocalScale(), afterScale);
                CheckRotation(fixture.TargetTransform().GetLocalRotation(), afterRotation);
                CHECK(fixture.Recorded.size() == 1);
            }
        }
    });
}

TEST_CASE("Editor action store skips empty and unsupported Scale and Rotation drags", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const auto mode : {Scale, Rotation})
        {
            for (const std::string reason : {"unchanged", "returned", "rect", "inactive", "equivalent_rotation"})
            {
                if (mode == Scale && reason == "equivalent_rotation") continue;
                CAPTURE(mode, reason);
                TransformDragFixture fixture;
                fixture.Begin(mode);
                const auto before = fixture.Control().DragStartTargetStates.front();
                if (reason != "unchanged")
                {
                    if (mode == Scale) fixture.TargetTransform().GetLocalScale() = {2, 3, 4};
                    else fixture.TargetTransform().SetRotation(math::Vector3(10, 20, 30));
                }
                if (reason == "returned")
                {
                    fixture.TargetTransform().GetLocalScale() = before.LocalScale;
                    fixture.TargetTransform().SetRotation(before.LocalRotation);
                }
                if (reason == "equivalent_rotation") fixture.TargetTransform().SetRotation(-before.LocalRotation);
                if (reason == "rect") fixture.Native.Registry->Get<ui::RectTransform>(fixture.Target);
                if (reason == "inactive")
                {
                    fixture.Control().RightScaleArrow.DragActive = false;
                    fixture.Control().RightRotationRing.DragActive = false;
                }
                fixture.Release();
                fixture.Finish.OnUpdate();
                CHECK(fixture.Recorded.empty());
                CHECK(fixture.Control().DragStartTargetStates.empty());
            }
        }
    });
}

TEST_CASE("Editor action store records one gizmo action for all changed entities", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const auto mode : {Movement, Scale, Rotation})
        {
            CAPTURE(mode);
            TransformDragFixture fixture;
            const auto child = fixture.Native.Entity(2);
            const auto unchanged = fixture.Native.Entity(3);
            fixture.Native.Registry->Get<Transform>(child).SetParent(fixture.Target);
            fixture.Native.Registry->Get<Transform>(child).GetLocalPosition() = {10, 11, 12};
            fixture.Control().TargetEntities = {fixture.Target, child, unchanged};
            fixture.Begin(mode);
            const auto beforeFirst = ReadState(fixture.TargetTransform());
            const auto beforeChild = ReadState(fixture.Native.Registry->Get<Transform>(child));
            const auto beforeChildWorldPosition = fixture.Native.Registry->Get<Transform>(child).GetWorldPosition();
            ChangeValue(fixture.TargetTransform(), mode, {4, 5, 6});
            ChangeValue(fixture.Native.Registry->Get<Transform>(child), mode, {7, 8, 9});
            const auto afterFirst = ReadState(fixture.TargetTransform());
            const auto afterChild = ReadState(fixture.Native.Registry->Get<Transform>(child));
            const auto afterChildWorldPosition = fixture.Native.Registry->Get<Transform>(child).GetWorldPosition();
            fixture.Finish.OnUpdate();
            REQUIRE(fixture.Recorded.empty());
            fixture.Release();
            REQUIRE(fixture.Recorded.size() == 1);
            fixture.Finish.OnUpdate();
            REQUIRE(fixture.Recorded.size() == 1);
            const auto id = fixture.Recorded.front();

            // An unchanged selection member must not be captured by this action.
            ChangeValue(fixture.Native.Registry->Get<Transform>(unchanged), mode, {20, 30, 40});
            const auto external = ReadState(fixture.Native.Registry->Get<Transform>(unchanged));
            REQUIRE(fixture.Actions->Undo(id));
            CheckState(fixture.TargetTransform(), beforeFirst);
            CheckState(fixture.Native.Registry->Get<Transform>(child), beforeChild);
            CheckVector(fixture.Native.Registry->Get<Transform>(child).GetWorldPosition(), beforeChildWorldPosition);
            CheckState(fixture.Native.Registry->Get<Transform>(unchanged), external);
            REQUIRE(fixture.Actions->Redo(id));
            CheckState(fixture.TargetTransform(), afterFirst);
            CheckState(fixture.Native.Registry->Get<Transform>(child), afterChild);
            CheckVector(fixture.Native.Registry->Get<Transform>(child).GetWorldPosition(), afterChildWorldPosition);
            CheckState(fixture.Native.Registry->Get<Transform>(unchanged), external);
            CHECK(fixture.Recorded.size() == 1);
        }
    });
}

TEST_CASE("Editor action store rejects invalid group replay before changing any target", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const auto mode : {Movement, Scale, Rotation})
        {
            for (const bool undo : {true, false})
            {
                CAPTURE(mode, undo);
                TransformDragFixture fixture;
                const auto second = fixture.Native.Entity(2);
                fixture.Control().TargetEntities.push_back(second);
                fixture.Begin(mode);
                ChangeValue(fixture.TargetTransform(), mode, {4, 5, 6});
                ChangeValue(fixture.Native.Registry->Get<Transform>(second), mode, {7, 8, 9});
                fixture.Release();
                REQUIRE(fixture.Recorded.size() == 1);
                const auto id = fixture.Recorded.front();
                if (!undo) REQUIRE(fixture.Actions->Undo(id));
                const auto firstBeforeReplay = ReadState(fixture.TargetTransform());
                fixture.Native.Registry->Del<Transform>(second);
                const bool applied = undo ? fixture.Actions->Undo(id) : fixture.Actions->Redo(id);
                CHECK_FALSE(applied);
                CheckState(fixture.TargetTransform(), firstBeforeReplay);
                CHECK(fixture.Recorded.size() == 1);
            }
        }
    });
}

TEST_CASE("Editor action store skips unchanged and unsupported groups", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const auto mode : {Movement, Scale, Rotation})
        {
            for (const bool rect : {false, true})
            {
                CAPTURE(mode, rect);
                TransformDragFixture fixture;
                const auto second = fixture.Native.Entity(2);
                fixture.Control().TargetEntities.push_back(second);
                fixture.Begin(mode);
                if (rect)
                {
                    fixture.Native.Registry->Get<ui::RectTransform>(second);
                    ChangeValue(fixture.TargetTransform(), mode, {4, 5, 6});
                }
                fixture.Release();
                CHECK(fixture.Recorded.empty());
            }
        }
    });
}

TEST_CASE("Editor action store cannot replay an empty transform action", "[native][editor-actions]")
{
    EntityMoveAction move(std::vector<EntityMoveAction::Change>{});
    EntityScaleAction scale(std::vector<EntityScaleAction::Change>{});
    EntityRotateAction rotate(std::vector<EntityRotateAction::Change>{});
    CHECK_FALSE(move.Undo());
    CHECK_FALSE(move.Redo());
    CHECK_FALSE(scale.Undo());
    CHECK_FALSE(scale.Redo());
    CHECK_FALSE(rotate.Undo());
    CHECK_FALSE(rotate.Redo());
}

TEST_CASE("Editor action store restores sibling orders after moving first and last entities", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        for (const bool nested : {false, true})
        {
            const auto parent = nested ? fixture.Entity(10) : ecs::NULL_ENTITY;
            std::vector<ecs::ComponentRef<Transform>> siblings;
            for (i32 i = 0; i < 3; i++)
            {
                siblings.emplace_back(fixture.Registry, fixture.Entity(i + 1));
                siblings.back().Get().SetParent(parent);
                siblings.back().Get().SetChildOrder(i);
            }
            for (const i32 destination : {2, 0})
            {
                std::vector<EntityOrderAction::Change> changes;
                for (const auto& target : siblings) changes.push_back({target, target.Get().GetChildOrder(), 0});
                siblings[0].Get().SetParent(parent, destination);
                for (auto& change : changes) change.After = change.Target.Get().GetChildOrder();
                const std::vector<i32> expected = destination == 2 ? std::vector<i32>{2, 0, 1} : std::vector<i32>{0, 1, 2};
                for (i32 i = 0; i < 3; i++) CHECK(siblings[i].Get().GetChildOrder() == expected[i]);
                EditorActionStore actions;
                i32 records = 0;
                actions.ActionRecorded.append([&](u64) { records++; });
                const auto id = actions.RecordApplied(std::make_unique<EntityOrderAction>(changes));
                REQUIRE(actions.Undo(id));
                for (const auto& change : changes) CHECK(change.Target.Get().GetChildOrder() == change.Before);
                REQUIRE(actions.Redo(id));
                for (const auto& change : changes)
                {
                    CHECK(change.Target.Get().GetChildOrder() == change.After);
                    CHECK(change.Target.Get().GetParent() == parent);
                }
                CHECK(records == 1);
            }
            siblings[0].Get().SetParent(parent, 100);
            CHECK(siblings[0].Get().GetChildOrder() == 2);
            siblings[0].Get().SetParent(parent, -100);
            CHECK(siblings[0].Get().GetChildOrder() == 0);
            // Separate parent groups for the next iteration.
            for (const auto& target : siblings) fixture.Registry->DestroyEntity(target.Get().GetEntity());
            fixture.World->RefreshAll();
        }
    });
}

TEST_CASE("Editor action store rejects missing order targets without partially applying", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        ecs::ComponentRef<Transform> target(fixture.Registry, fixture.Entity());
        target.Get().SetChildOrder(2);
        EntityOrderAction action({{target, 0, 2}, {{}, 1, 0}});
        CHECK_FALSE(action.Undo());
        CHECK(target.Get().GetChildOrder() == 2);
        CHECK_FALSE(action.Redo());
        CHECK(target.Get().GetChildOrder() == 2);
        CHECK_FALSE(EntityOrderAction({}).Undo());
    });
}

TEST_CASE("Editor action store restores reparented entity and both sibling groups", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (i32 mode = 0; mode < 3; mode++)
        {
            BehaviourFixture fixture;
            const auto firstParent = fixture.Entity(10);
            const auto secondParent = fixture.Entity(11);
            auto& first = fixture.Registry->Get<Transform>(firstParent);
            first.GetLocalPosition() = {10, 20, 30};
            first.GetLocalScale() = {2, 2, 2};
            first.SetRotation(math::Vector3(10, 20, 30));
            auto& second = fixture.Registry->Get<Transform>(secondParent);
            second.GetLocalPosition() = {-5, 7, 3};
            second.GetLocalScale() = {3, 3, 3};
            second.SetRotation(math::Vector3(30, 0, 45));
            const auto oldParent = mode == 2 ? ecs::NULL_ENTITY : firstParent;
            const auto newParent = mode == 1 ? ecs::NULL_ENTITY : secondParent;
            ecs::ComponentRef<Transform> target(fixture.Registry, fixture.Entity(1));
            target.Get().SetParent(oldParent);
            target.Get().GetLocalPosition() = {1, 2, 3};
            target.Get().GetLocalScale() = {1, 2, 3};
            target.Get().SetRotation(math::Vector3(15, 25, 35));
            for (i32 i = 0; i < 4; i++)
            {
                auto& sibling = fixture.Registry->Get<Transform>(fixture.Entity(i + 2));
                sibling.SetParent(i < 2 ? oldParent : newParent);
                sibling.SetChildOrder(i < 2 ? i + 1 : i - 2);
            }
            transform_utility::NormalizeSiblingOrders(oldParent);
            transform_utility::NormalizeSiblingOrders(newParent);
            const EntityReparentAction::State before(target.Get());
            const auto beforeOrder = target.Get().GetChildOrder();
            const auto worldPosition = target.Get().GetWorldPosition();
            const auto worldScale = target.Get().GetWorldScale();
            const auto worldRotation = target.Get().GetWorldRotation();
            auto siblings = transform_utility::CollectSiblings(oldParent);
            const auto destination = transform_utility::CollectSiblings(newParent);
            siblings.insert(siblings.end(), destination.begin(), destination.end());
            std::vector<EntityOrderAction::Change> orders;
            for (const auto entity : siblings)
            {
                ecs::ComponentRef<Transform> sibling(fixture.Registry, entity);
                orders.push_back({sibling, sibling.Get().GetChildOrder(), 0});
            }

            target.Get().SetParent(newParent, 1);
            CHECK(target.Get().GetParent() == newParent);
            CHECK(target.Get().GetChildOrder() == 1);
            CheckVector(target.Get().GetWorldPosition(), worldPosition);
            CheckVector(target.Get().GetWorldScale(), worldScale);
            CheckRotation(target.Get().GetWorldRotation(), worldRotation);
            const EntityReparentAction::State after(target.Get());
            for (auto& change : orders) change.After = change.Target.Get().GetChildOrder();
            for (const auto parent : {oldParent, newParent})
            {
                std::vector<i32> actual;
                for (const auto sibling : transform_utility::CollectSiblings(parent)) actual.push_back(fixture.Registry->Get<Transform>(sibling).GetChildOrder());
                std::ranges::sort(actual);
                for (i32 i = 0; i < actual.size(); i++) CHECK(actual[i] == i);
            }
            EditorActionStore actions;
            i32 records = 0;
            actions.ActionRecorded.append([&](u64) { records++; });
            const auto id = actions.RecordApplied(std::make_unique<EntityReparentAction>(target, before, after, orders));
            REQUIRE(actions.Undo(id));
            CHECK(target.Get().GetParent() == oldParent);
            CHECK(target.Get().GetChildOrder() == beforeOrder);
            CheckVector(target.Get().GetLocalPosition(), before.Position);
            CheckVector(target.Get().GetLocalScale(), before.Scale);
            CheckRotation(target.Get().GetLocalRotation(), before.Rotation);
            CheckVector(target.Get().GetWorldPosition(), worldPosition);
            for (const auto& change : orders) CHECK(change.Target.Get().GetChildOrder() == change.Before);
            REQUIRE(actions.Redo(id));
            CHECK(target.Get().GetParent() == newParent);
            CheckVector(target.Get().GetLocalPosition(), after.Position);
            CheckVector(target.Get().GetLocalScale(), after.Scale);
            CheckRotation(target.Get().GetLocalRotation(), after.Rotation);
            CheckVector(target.Get().GetWorldPosition(), worldPosition);
            for (const auto& change : orders) CHECK(change.Target.Get().GetChildOrder() == change.After);
            CHECK(records == 1);
        }
    });
}

TEST_CASE("Editor action store rejects missing reparent targets before making changes", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(1);
        ecs::ComponentRef<Transform> target(fixture.Registry, fixture.Entity(2));
        target.Get().SetParent(parent);
        const EntityReparentAction::State before(target.Get());
        target.Get().SetParent(ecs::NULL_ENTITY);
        target.Get().GetLocalPosition() = {7, 8, 9};
        const EntityReparentAction::State after(target.Get());
        EntityReparentAction missingSibling(target, before, after, {{{}, 0, 1}});
        CHECK_FALSE(missingSibling.Undo());
        CHECK(target.Get().GetParent() == ecs::NULL_ENTITY);
        CheckVector(target.Get().GetLocalPosition(), after.Position);
        EntityReparentAction missingParent(target, before, after, {});
        fixture.Registry->DestroyEntity(parent);
        fixture.World->Refresh();
        CHECK_FALSE(missingParent.Undo());
        CHECK(target.Get().GetParent() == ecs::NULL_ENTITY);
        CheckVector(target.Get().GetLocalPosition(), after.Position);
        EntityReparentAction missingTarget({}, before, after, {});
        CHECK_FALSE(missingTarget.Undo());
        CHECK_FALSE(missingTarget.Redo());
    });
}

TEST_CASE("Editor action store restores grouped sibling moves with stable selection order", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        struct Case
        {
            std::vector<i32> Selected;
            i32 Insertion;
            std::vector<i32> Expected;
        };
        const std::vector<Case> cases = {
            {{1, 2}, 5, {0, 3, 4, 1, 2}},
            {{3, 1}, 5, {0, 2, 4, 1, 3}},
            {{3, 4}, 0, {3, 4, 0, 1, 2}},
            {{1, 3}, 3, {0, 2, 1, 3, 4}},
            {{1, 2}, 2, {0, 1, 2, 3, 4}},
            {{4, 3, 2, 1, 0}, 3, {0, 1, 2, 3, 4}},
            {{1, 3}, -100, {1, 3, 0, 2, 4}},
            {{1, 3}, 100, {0, 2, 4, 1, 3}}
        };
        for (const bool nested : {false, true})
        {
            for (const auto& scenario : cases)
            {
                BehaviourFixture fixture;
                const auto parent = nested ? fixture.Entity(10) : ecs::NULL_ENTITY;
                std::vector<ecs::Entity> siblings;
                std::vector<EntityOrderAction::Change> orders;
                for (i32 i = 0; i < 5; i++)
                {
                    const auto entity = fixture.Entity(i + 1);
                    ecs::ComponentRef<Transform> transform(fixture.Registry, entity);
                    transform.Get().SetParent(parent);
                    transform.Get().SetChildOrder(i);
                    transform.Get().GetLocalPosition() = {static_cast<f32>(i), 2, 3};
                    siblings.push_back(entity);
                    orders.push_back({transform, i, 0});
                }
                std::vector<ecs::Entity> selected;
                for (const auto index : scenario.Selected) selected.push_back(siblings[index]);
                const bool changed = transform_utility::ReorderSiblings(selected, scenario.Insertion);
                for (i32 index = 0; index < 5; index++)
                {
                    auto& transform = fixture.Registry->Get<Transform>(siblings[scenario.Expected[index]]);
                    CHECK(transform.GetChildOrder() == index);
                    CHECK(transform.GetParent() == parent);
                    CheckVector(transform.GetLocalPosition(), {static_cast<f32>(scenario.Expected[index]), 2, 3});
                }
                CHECK(changed == (scenario.Expected != std::vector<i32>{0, 1, 2, 3, 4}));
                if (!changed) continue;
                for (auto& order : orders) order.After = order.Target.Get().GetChildOrder();
                EditorActionStore actions;
                i32 records = 0;
                actions.ActionRecorded.append([&](u64) { records++; });
                const auto id = actions.RecordApplied(std::make_unique<EntityOrderAction>(orders));
                REQUIRE(actions.Undo(id));
                for (const auto& order : orders) CHECK(order.Target.Get().GetChildOrder() == order.Before);
                REQUIRE(actions.Redo(id));
                for (const auto& order : orders) CHECK(order.Target.Get().GetChildOrder() == order.After);
                CHECK(records == 1);
            }
        }
    });
}

TEST_CASE("Editor action store grouped sibling move rejects invalid groups without mutation", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(1);
        const auto second = fixture.Entity(2);
        const auto parent = fixture.Entity(10);
        fixture.Registry->Get<Transform>(first).SetChildOrder(0);
        fixture.Registry->Get<Transform>(second).SetChildOrder(1);
        fixture.Registry->Get<Transform>(second).SetParent(parent);
        CHECK_FALSE(transform_utility::ReorderSiblings({}, 0));
        CHECK_FALSE(transform_utility::ReorderSiblings({first, ecs::NULL_ENTITY}, 0));
        CHECK_FALSE(transform_utility::ReorderSiblings({first, first}, 2));
        CHECK_FALSE(transform_utility::ReorderSiblings({first, second}, 0));
        CHECK(fixture.Registry->Get<Transform>(first).GetChildOrder() == 0);
        CHECK(fixture.Registry->Get<Transform>(second).GetChildOrder() == 1);
        CHECK(fixture.Registry->Get<Transform>(second).GetParent() == parent);
    });
}

TEST_CASE("Editor action store restores grouped reparent local transforms and both sibling groups", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        struct WorldState
        {
            math::Vector3 Position;
            math::Vector3 Scale;
            glm::quat Rotation;
        };
        for (i32 mode = 0; mode < 3; mode++)
        {
            for (const std::vector<i32> indices : {std::vector<i32>{0, 1}, std::vector<i32>{0, 2}, std::vector<i32>{0, 1, 2}})
            {
                for (const i32 insertion : {-10, 0, 1, 100})
                {
                    BehaviourFixture fixture;
                    const auto firstParent = fixture.Entity(10);
                    const auto secondParent = fixture.Entity(11);
                    auto& first = fixture.Registry->Get<Transform>(firstParent);
                    first.GetLocalPosition() = {10, 20, 30};
                    first.GetLocalScale() = {2, 2, 2};
                    first.SetRotation(math::Vector3(0, 0, 30));
                    auto& second = fixture.Registry->Get<Transform>(secondParent);
                    second.GetLocalPosition() = {-5, 7, 3};
                    second.GetLocalScale() = {3, 3, 3};
                    second.SetRotation(math::Vector3(0, 0, -45));
                    const auto oldParent = mode == 2 ? ecs::NULL_ENTITY : firstParent;
                    const auto newParent = mode == 1 ? ecs::NULL_ENTITY : secondParent;
                    std::vector<ecs::Entity> source;
                    for (i32 i = 0; i < 5; i++)
                    {
                        const auto entity = fixture.Entity(i + 1);
                        auto& transform = fixture.Registry->Get<Transform>(entity);
                        transform.SetParent(i < 3 ? oldParent : newParent);
                        transform.SetChildOrder(i < 3 ? i : i - 3);
                        transform.GetLocalPosition() = {static_cast<f32>(i + 1), 2, 3};
                        transform.GetLocalScale() = {1.5f, 1.5f, 1.5f};
                        transform.SetRotation(math::Vector3(0, 0, 15 * static_cast<f32>(i)));
                        if (i < 3) source.push_back(entity);
                    }
                    transform_utility::NormalizeSiblingOrders(oldParent);
                    transform_utility::NormalizeSiblingOrders(newParent);
                    const i32 expectedIndex = std::clamp(insertion, 0, static_cast<i32>(transform_utility::CollectSiblings(newParent).size()));
                    std::vector<ecs::Entity> selected;
                    std::vector<EntityReparentAction::Change> changes;
                    std::vector<WorldState> world;
                    for (const auto index : indices)
                    {
                        const auto entity = source[index];
                        selected.push_back(entity);
                        ecs::ComponentRef<Transform> target(fixture.Registry, entity);
                        const EntityReparentAction::State before(target.Get());
                        changes.push_back({target, before, before});
                        world.push_back({target.Get().GetWorldPosition(), target.Get().GetWorldScale(), target.Get().GetWorldRotation()});
                    }
                    auto siblings = transform_utility::CollectSiblings(oldParent);
                    const auto destination = transform_utility::CollectSiblings(newParent);
                    siblings.insert(siblings.end(), destination.begin(), destination.end());
                    std::vector<EntityOrderAction::Change> orders;
                    for (const auto entity : siblings)
                    {
                        ecs::ComponentRef<Transform> target(fixture.Registry, entity);
                        orders.push_back({target, target.Get().GetChildOrder(), 0});
                    }
                    REQUIRE(transform_utility::ReparentEntities(selected, newParent, insertion));
                    for (i32 i = 0; i < static_cast<i32>(changes.size()); i++)
                    {
                        auto& target = changes[i].Target.Get();
                        CHECK(target.GetParent() == newParent);
                        CHECK(target.GetChildOrder() == expectedIndex + i);
                        CheckVector(target.GetWorldPosition(), world[i].Position);
                        CheckVector(target.GetWorldScale(), world[i].Scale);
                        CheckRotation(target.GetWorldRotation(), world[i].Rotation);
                        changes[i].After = EntityReparentAction::State(target);
                    }
                    for (auto& order : orders) order.After = order.Target.Get().GetChildOrder();
                    EditorActionStore actions;
                    i32 records = 0;
                    actions.ActionRecorded.append([&](u64) { records++; });
                    const auto id = actions.RecordApplied(std::make_unique<EntityReparentAction>(changes, orders));
                    REQUIRE(actions.Undo(id));
                    for (const auto& change : changes)
                    {
                        CHECK(change.Target.Get().GetParent() == oldParent);
                        CheckVector(change.Target.Get().GetLocalPosition(), change.Before.Position);
                        CheckVector(change.Target.Get().GetLocalScale(), change.Before.Scale);
                        CheckRotation(change.Target.Get().GetLocalRotation(), change.Before.Rotation);
                    }
                    for (const auto& order : orders) CHECK(order.Target.Get().GetChildOrder() == order.Before);
                    REQUIRE(actions.Redo(id));
                    for (const auto& change : changes)
                    {
                        CHECK(change.Target.Get().GetParent() == newParent);
                        CheckVector(change.Target.Get().GetLocalPosition(), change.After.Position);
                        CheckVector(change.Target.Get().GetLocalScale(), change.After.Scale);
                        CheckRotation(change.Target.Get().GetLocalRotation(), change.After.Rotation);
                    }
                    for (const auto& order : orders) CHECK(order.Target.Get().GetChildOrder() == order.After);
                    CHECK(records == 1);
                }
            }
        }
    });
}

TEST_CASE("Editor action store grouped reparent rejects invalid input and never partially replays", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(10);
        const auto first = fixture.Entity(1);
        const auto second = fixture.Entity(2);
        const auto child = fixture.Entity(3);
        fixture.Registry->Get<Transform>(child).SetParent(second);
        fixture.Registry->Get<Transform>(second).SetChildOrder(1);
        CHECK_FALSE(transform_utility::ReparentEntities({}, parent, 0));
        CHECK_FALSE(transform_utility::ReparentEntities({first, ecs::NULL_ENTITY}, parent, 0));
        CHECK_FALSE(transform_utility::ReparentEntities({first, first}, parent, 0));
        CHECK_FALSE(transform_utility::ReparentEntities({first, second}, second, 0));
        CHECK_FALSE(transform_utility::ReparentEntities({first, second}, child, 0));
        fixture.Registry->Get<Transform>(second).SetParent(parent);
        CHECK_FALSE(transform_utility::ReparentEntities({second, child}, ecs::NULL_ENTITY, 0));
        fixture.Registry->Get<Transform>(second).SetParent(ecs::NULL_ENTITY);
        fixture.Registry->Get<ui::RectTransform>(second);
        CHECK_FALSE(transform_utility::ReparentEntities({first, second}, parent, 0));
        CHECK(fixture.Registry->Get<Transform>(first).GetParent() == ecs::NULL_ENTITY);
        CHECK(fixture.Registry->Get<Transform>(second).GetParent() == ecs::NULL_ENTITY);
        CHECK(fixture.Registry->Get<Transform>(second).GetChildOrder() == 1);

        ecs::ComponentRef<Transform> target(fixture.Registry, first);
        const EntityReparentAction::State before(target.Get());
        target.Get().SetParent(parent);
        target.Get().GetLocalPosition() = {7, 8, 9};
        const EntityReparentAction::State after(target.Get());
        EntityReparentAction missingTarget(std::vector<EntityReparentAction::Change>{{target, before, after}, {{}, before, after}}, {});
        CHECK_FALSE(missingTarget.Undo());
        CHECK_FALSE(missingTarget.Redo());
        CHECK(target.Get().GetParent() == parent);
        CheckVector(target.Get().GetLocalPosition(), after.Position);
        EntityReparentAction missingOrder(std::vector<EntityReparentAction::Change>{{target, before, after}}, {{{}, 0, 1}});
        CHECK_FALSE(missingOrder.Undo());
        CHECK(target.Get().GetParent() == parent);
        CheckVector(target.Get().GetLocalPosition(), after.Position);
        CHECK_FALSE(EntityReparentAction(std::vector<EntityReparentAction::Change>{}, {}).Undo());
    });
}

TEST_CASE("Editor action store mixed parent move preserves input order and restores every sibling group", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        struct WorldState
        {
            math::Vector3 Position;
            math::Vector3 Scale;
            glm::quat Rotation;
        };
        for (i32 mode = 0; mode < 4; mode++)
        {
            BehaviourFixture fixture;
            std::vector<ecs::Entity> parents;
            for (i32 i = 0; i < 3; i++)
            {
                const auto entity = fixture.Entity(10 + i * 10);
                parents.push_back(entity);
                auto& transform = fixture.Registry->Get<Transform>(entity);
                transform.SetChildOrder(i);
                transform.GetLocalPosition() = {10 * static_cast<f32>(i + 1), 2, 3};
                const auto scale = static_cast<f32>(i + 2);
                transform.GetLocalScale() = {scale, scale, scale};
                transform.SetRotation(math::Vector3(0, 0, 15 * static_cast<f32>(i + 1)));
            }
            const auto first = fixture.Entity(1);
            const auto second = fixture.Entity(4);
            const auto source = mode == 3 ? ecs::NULL_ENTITY : parents[0];
            auto& a = fixture.Registry->Get<Transform>(first);
            a.SetParent(source);
            a.SetChildOrder(1);
            a.GetLocalPosition() = {1, 2, 3};
            a.SetRotation(math::Vector3(0, 0, 10));
            auto& b = fixture.Registry->Get<Transform>(second);
            b.SetParent(parents[1]);
            b.GetLocalPosition() = {4, 5, 6};
            b.GetLocalScale() = {1.5f, 1.5f, 1.5f};
            b.SetRotation(math::Vector3(0, 0, -20));
            for (const auto id : {2, 3, 5, 6, 7})
            {
                auto& transform = fixture.Registry->Get<Transform>(fixture.Entity(id));
                transform.SetParent(id < 4 ? parents[0] : id == 5 ? parents[1] : parents[2]);
                transform.SetChildOrder(id == 2 || id == 6 ? 0 : id == 3 ? 2 : 1);
            }
            const auto destination = mode == 1 ? ecs::NULL_ENTITY : mode == 2 ? parents[1] : parents[2];
            std::vector<EntityOrderAction::Change> orders;
            for (const auto parent : {ecs::NULL_ENTITY, parents[0], parents[1], parents[2]})
            {
                transform_utility::NormalizeSiblingOrders(parent);
                for (const auto sibling : transform_utility::CollectSiblings(parent))
                {
                    ecs::ComponentRef<Transform> target(fixture.Registry, sibling);
                    orders.push_back({target, target.Get().GetChildOrder(), 0});
                }
            }
            std::vector<EntityReparentAction::Change> changes;
            std::vector<WorldState> world;
            for (const auto entity : {first, second})
            {
                ecs::ComponentRef<Transform> target(fixture.Registry, entity);
                const EntityReparentAction::State before(target.Get());
                changes.push_back({target, before, before});
                world.push_back({target.Get().GetWorldPosition(), target.Get().GetWorldScale(), target.Get().GetWorldRotation()});
            }
            REQUIRE(transform_utility::ReparentEntities({first, second}, destination, 1));
            const i32 index = mode == 2 ? 0 : 1;
            for (i32 i = 0; i < 2; i++)
            {
                const auto& target = changes[i].Target.Get();
                CHECK(target.GetParent() == destination);
                CHECK(target.GetChildOrder() == index + i);
                CheckVector(target.GetWorldPosition(), world[i].Position);
                CheckVector(target.GetWorldScale(), world[i].Scale);
                CheckRotation(target.GetWorldRotation(), world[i].Rotation);
                changes[i].After = EntityReparentAction::State(target);
            }
            if (mode == 2)
            {
                CheckVector(changes[1].After.Position, changes[1].Before.Position);
                CheckVector(changes[1].After.Scale, changes[1].Before.Scale);
                CheckRotation(changes[1].After.Rotation, changes[1].Before.Rotation);
            }
            for (auto& order : orders) order.After = order.Target.Get().GetChildOrder();
            EditorActionStore actions;
            i32 records = 0;
            actions.ActionRecorded.append([&](u64) { records++; });
            const auto id = actions.RecordApplied(std::make_unique<EntityReparentAction>(changes, orders));
            REQUIRE(actions.Undo(id));
            for (const auto& change : changes)
            {
                CHECK(change.Target.Get().GetParent() == change.Before.Parent);
                CheckVector(change.Target.Get().GetLocalPosition(), change.Before.Position);
                CheckVector(change.Target.Get().GetLocalScale(), change.Before.Scale);
                CheckRotation(change.Target.Get().GetLocalRotation(), change.Before.Rotation);
            }
            for (const auto& order : orders) CHECK(order.Target.Get().GetChildOrder() == order.Before);
            REQUIRE(actions.Redo(id));
            for (i32 i = 0; i < 2; i++)
            {
                const auto& target = changes[i].Target.Get();
                CHECK(target.GetParent() == destination);
                CheckVector(target.GetWorldPosition(), world[i].Position);
                CheckVector(target.GetWorldScale(), world[i].Scale);
                CheckRotation(target.GetWorldRotation(), world[i].Rotation);
            }
            for (const auto& order : orders) CHECK(order.Target.Get().GetChildOrder() == order.After);
            CHECK_FALSE(transform_utility::ReparentEntities({first, second}, destination, 1));
            CHECK(records == 1);
        }
    });
}

TEST_CASE("Editor action store RectTransform move restores only anchored position", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        RectMoveFixture fixture;
        ecs::ComponentRef<ui::RectTransform> target(fixture.Native.Registry, fixture.Target);
        target.Get().GetAnchoredPosition() = {34, 36};
        const auto id = fixture.Actions->RecordApplied(std::make_unique<RectTransformMoveAction>(target, math::Vector2(10, 20), math::Vector2(34, 36)));
        target.Get().GetSizeDelta() = {250, 190};
        const nlohmann::json vector = {{"Value", {{"x", {{"Value", 0.25f}}}, {"y", {{"Value", 0.75f}}}}}};
        target.Get().REI_SET({{"_anchorMin", vector}, {"_anchorMax", vector}, {"_pivot", vector}});
        fixture.TargetTransform().GetLocalScale() = {2, 3, 4};
        fixture.TargetTransform().SetRotation(math::Vector3(10, 20, 30));
        const auto transform = ReadState(fixture.TargetTransform());
        for (const bool undo : {true, false})
        {
            REQUIRE((undo ? fixture.Actions->Undo(id) : fixture.Actions->Redo(id)));
            CHECK(glm::vec2(target.Get().GetAnchoredPosition()) == glm::vec2(undo ? math::Vector2(10, 20) : math::Vector2(34, 36)));
            CHECK(glm::vec2(target.Get().GetSizeDelta()) == glm::vec2(250, 190));
            CHECK(glm::vec2(target.Get().GetAnchorMin()) == glm::vec2(0.25f, 0.75f));
            CHECK(glm::vec2(target.Get().GetAnchorMax()) == glm::vec2(0.25f, 0.75f));
            CHECK(glm::vec2(target.Get().GetPivot()) == glm::vec2(0.25f, 0.75f));
            CheckState(fixture.TargetTransform(), transform);
        }
        CHECK(fixture.Recorded.size() == 1);
        fixture.Native.Registry->Del<ui::RectTransform>(fixture.Target);
        CHECK_FALSE(fixture.Actions->Undo(id));
        CHECK_FALSE(fixture.Actions->Redo(id));
    });
}

TEST_CASE("Editor action store records one RectTransform move for arrows plane and body on release", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (i32 kind = 0; kind < 4; kind++)
        {
            CAPTURE(kind);
            RectMoveFixture fixture;
            fixture.Press(kind);
            fixture.Input.Cursor(fixture.Input.First, 354, 204);
            fixture.Update(kind);
            fixture.Finish.OnUpdate();
            CHECK(fixture.Recorded.empty());
            const auto after = fixture.Rect().GetAnchoredPosition();
            CHECK(after.x == Catch::Approx(kind == 1 ? 10 : 34));
            CHECK(after.y == Catch::Approx(kind == 0 ? 20 : 36));
            fixture.Release();
            REQUIRE(fixture.Recorded.size() == 1);
            fixture.Finish.OnUpdate();
            CHECK(fixture.Recorded.size() == 1);
            CHECK(fixture.Control().DragStartTargetStates.empty());
            CHECK_FALSE(fixture.Control().RectTransformBodyDragActive);
            REQUIRE(fixture.Actions->Undo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(10, 20));
            REQUIRE(fixture.Actions->Redo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(after));
            CHECK(fixture.Recorded.size() == 1);
        }
    });
}

TEST_CASE("Editor action store skips RectTransform move no-ops inactive and incomplete drags", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const std::string reason : {"unchanged", "returned", "click", "scale", "rotation", "inactive", "partial_group", "missing"})
        {
            CAPTURE(reason);
            RectMoveFixture fixture;
            const bool body = reason == "click";
            fixture.Control().Mode = body ? editor::RectTransform : editor::Movement;
            fixture.Control().RightMovementArrow.DragActive = !body;
            fixture.Control().RectTransformBodyDragPending = reason == "click";
            if (reason == "partial_group")
            {
                const auto second = fixture.Native.Registry->NewEntity();
                fixture.Control().TargetEntities.push_back(second);
            }
            transformation_control_drag::CaptureDragStartTargetStates(fixture.Native.World, fixture.Control());
            if (reason != "unchanged" && reason != "click") fixture.Rect().GetAnchoredPosition() = {34, 36};
            if (reason == "returned") fixture.Rect().GetAnchoredPosition() = {10, 20};
            if (reason == "scale") fixture.Control().Mode = editor::Scale;
            if (reason == "rotation") fixture.Control().Mode = editor::Rotation;
            if (reason == "inactive") fixture.Control().RightMovementArrow.DragActive = false;
            if (reason == "missing")
            {
                fixture.Native.Registry->DestroyEntity(fixture.Target);
                fixture.Native.World->Refresh();
            }
            fixture.Release();
            fixture.Finish.OnUpdate();
            CHECK(fixture.Recorded.empty());
        }
    });
}

TEST_CASE("Editor action store RectTransform resize restores position and size without changing other fields", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        RectMoveFixture fixture;
        ecs::ComponentRef<ui::RectTransform> target(fixture.Native.Registry, fixture.Target);
        const RectTransformResizeAction::State before{{10, 20}, {200, 160}};
        const RectTransformResizeAction::State after{{22, 28}, {224, 176}};
        fixture.Rect().GetAnchoredPosition() = after.AnchoredPosition;
        fixture.Rect().GetSizeDelta() = after.SizeDelta;
        const auto id = fixture.Actions->RecordApplied(std::make_unique<RectTransformResizeAction>(target, before, after));
        const nlohmann::json vector = {{"Value", {{"x", {{"Value", 0.25f}}}, {"y", {{"Value", 0.75f}}}}}};
        fixture.Rect().REI_SET({{"_anchorMin", vector}, {"_anchorMax", vector}, {"_pivot", vector}});
        fixture.TargetTransform().GetLocalScale() = {2, 3, 4};
        fixture.TargetTransform().SetRotation(math::Vector3(10, 20, 30));
        const auto transform = ReadState(fixture.TargetTransform());
        for (const bool undo : {true, false})
        {
            REQUIRE((undo ? fixture.Actions->Undo(id) : fixture.Actions->Redo(id)));
            const auto& expected = undo ? before : after;
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(expected.AnchoredPosition));
            CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(expected.SizeDelta));
            CHECK(glm::vec2(fixture.Rect().GetAnchorMin()) == glm::vec2(0.25f, 0.75f));
            CHECK(glm::vec2(fixture.Rect().GetAnchorMax()) == glm::vec2(0.25f, 0.75f));
            CHECK(glm::vec2(fixture.Rect().GetPivot()) == glm::vec2(0.25f, 0.75f));
            CheckState(fixture.TargetTransform(), transform);
        }
        CHECK(fixture.Recorded.size() == 1);
        fixture.Native.Registry->DestroyEntity(fixture.Target);
        fixture.Native.World->Refresh();
        CHECK_FALSE(fixture.Actions->Undo(id));
        CHECK_FALSE(fixture.Actions->Redo(id));
    });
}

TEST_CASE("Editor action store records one RectTransform resize for single targets and groups at every edge and corner", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const i32 count : {1, 3})
        {
            for (i32 index = 0; index < 8; index++)
            {
                CAPTURE(count, index);
                RectMoveFixture fixture;
                if (count == 3)
                {
                    fixture.AddRectTarget(2, {40, 50});
                    fixture.AddRectTarget(3, {-30, 70});
                }
                const auto targets = fixture.Control().TargetEntities;
                const math::Vector2 positions[] = {{10, 20}, {40, 50}, {-30, 70}};
                fixture.PressResize(index);
                REQUIRE(fixture.ResizeHandle(index).DragActive);
                const auto direction = fixture.ResizeHandle(index).Direction;
                const auto check = [&](const bool resized)
                {
                    for (i32 i = 0; i < count; i++)
                    {
                        const auto& rect = fixture.Native.Registry->Get<ui::RectTransform>(targets[i]);
                        const math::Vector2 positionDelta(resized && direction.x != 0 ? 12.0f : 0.0f, resized && direction.y != 0 ? 8.0f : 0.0f);
                        const math::Vector2 initialSize = i == 0 ? math::Vector2(200, 160) : math::Vector2(100, 80);
                        const math::Vector2 sizeDelta = resized ? math::Vector2(direction.x * 24, direction.y * 16) : math::Vector2();
                        CHECK(glm::vec2(rect.GetAnchoredPosition()) == glm::vec2(positions[i] + positionDelta));
                        CHECK(glm::vec2(rect.GetSizeDelta()) == glm::vec2(initialSize + sizeDelta));
                    }
                };
                fixture.Input.Cursor(fixture.Input.First, 354, 204);
                fixture.Body.OnUpdate();
                fixture.Finish.OnUpdate();
                CHECK(fixture.Recorded.empty());
                check(true);
                fixture.Release();
                REQUIRE(fixture.Recorded.size() == 1);
                fixture.Finish.OnUpdate();
                CHECK(fixture.Recorded.size() == 1);
                CHECK_FALSE(fixture.ResizeHandle(index).DragActive);
                CHECK(fixture.Control().DragStartTargetStates.empty());
                REQUIRE(fixture.Actions->Undo(fixture.Recorded[0]));
                check(false);
                REQUIRE(fixture.Actions->Redo(fixture.Recorded[0]));
                check(true);
                CHECK(fixture.Recorded.size() == 1);
            }
        }
    });
}

TEST_CASE("Editor action store skips RectTransform resize no-ops inactive and incomplete drags", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const std::string reason : {"click", "returned", "inactive", "partial_group", "missing"})
        {
            CAPTURE(reason);
            RectMoveFixture fixture;
            if (reason == "partial_group")
            {
                const auto second = fixture.Native.Registry->NewEntity();
                fixture.Control().TargetEntities.push_back(second);
            }
            fixture.PressResize(2);
            if (reason != "click")
            {
                fixture.Input.Cursor(fixture.Input.First, 354, 204);
                fixture.Body.OnUpdate();
            }
            if (reason == "returned")
            {
                fixture.Input.Cursor(fixture.Input.First, 330, 220);
                fixture.Body.OnUpdate();
            }
            if (reason == "inactive") fixture.ResizeHandle(2).DragActive = false;
            if (reason == "missing")
            {
                fixture.Native.Registry->DestroyEntity(fixture.Target);
                fixture.Native.World->Refresh();
            }
            fixture.Release();
            fixture.Finish.OnUpdate();
            CHECK(fixture.Recorded.empty());
        }
    });
}

TEST_CASE("Editor action store RectTransform resize records size-only and clamped changes", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const bool clamped : {false, true})
        {
            RectMoveFixture fixture;
            fixture.Rect().REI_SET({{"_pivot", {{"Value", {{"x", {{"Value", 0}}}, {"y", {{"Value", 0}}}}}}}});
            fixture.PressResize(2);
            fixture.Input.Cursor(fixture.Input.First, clamped ? -670 : 354, clamped ? 1220 : 204);
            fixture.Body.OnUpdate();
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(10, 20));
            const math::Vector2 size = clamped ? math::Vector2(4, 4) : math::Vector2(224, 176);
            CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(size));
            fixture.Release();
            REQUIRE(fixture.Recorded.size() == 1);
            REQUIRE(fixture.Actions->Undo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(200, 160));
            REQUIRE(fixture.Actions->Redo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(size));
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(10, 20));
        }
    });
}

TEST_CASE("Editor action store records one RectTransform group move for arrows plane and body", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (i32 kind = 0; kind < 4; kind++)
        {
            CAPTURE(kind);
            RectMoveFixture fixture;
            fixture.AddRectTarget(2, {40, 50});
            fixture.AddRectTarget(3, {-30, 70});
            const auto targets = fixture.Control().TargetEntities;
            const math::Vector2 before[] = {{10, 20}, {40, 50}, {-30, 70}};
            fixture.Press(kind);
            fixture.Input.Cursor(fixture.Input.First, 354, 204);
            fixture.Update(kind);
            fixture.Finish.OnUpdate();
            CHECK(fixture.Recorded.empty());
            const math::Vector2 delta(kind == 1 ? 0.0f : 24.0f, kind == 0 ? 0.0f : 16.0f);
            for (i32 i = 0; i < 3; i++)
                CHECK(glm::vec2(fixture.Native.Registry->Get<ui::RectTransform>(targets[i]).GetAnchoredPosition()) == glm::vec2(before[i] + delta));
            fixture.Release();
            REQUIRE(fixture.Recorded.size() == 1);
            fixture.Finish.OnUpdate();
            CHECK(fixture.Recorded.size() == 1);
            REQUIRE(fixture.Actions->Undo(fixture.Recorded[0]));
            for (i32 i = 0; i < 3; i++)
                CHECK(glm::vec2(fixture.Native.Registry->Get<ui::RectTransform>(targets[i]).GetAnchoredPosition()) == glm::vec2(before[i]));
            REQUIRE(fixture.Actions->Redo(fixture.Recorded[0]));
            for (i32 i = 0; i < 3; i++)
            {
                auto& rect = fixture.Native.Registry->Get<ui::RectTransform>(targets[i]);
                CHECK(glm::vec2(rect.GetAnchoredPosition()) == glm::vec2(before[i] + delta));
                CHECK(glm::vec2(rect.GetSizeDelta()) == glm::vec2(i == 0 ? math::Vector2(200, 160) : math::Vector2(100, 80)));
            }
            CHECK(fixture.Recorded.size() == 1);
        }
    });
}

TEST_CASE("Editor action store RectTransform group move rejects missing targets before applying any value", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        RectMoveFixture fixture;
        const auto second = fixture.AddRectTarget(2, {40, 50});
        ecs::ComponentRef<ui::RectTransform> first(fixture.Native.Registry, fixture.Target);
        ecs::ComponentRef<ui::RectTransform> other(fixture.Native.Registry, second);
        RectTransformMoveAction action(std::vector<RectTransformMoveAction::Change>{{first, {10, 20}, {34, 36}}, {other, {40, 50}, {64, 66}}});
        first.Get().GetAnchoredPosition() = {34, 36};
        fixture.Native.Registry->Del<ui::RectTransform>(second);
        CHECK_FALSE(action.Undo());
        CHECK(glm::vec2(first.Get().GetAnchoredPosition()) == glm::vec2(34, 36));
        first.Get().GetAnchoredPosition() = {10, 20};
        CHECK_FALSE(action.Redo());
        CHECK(glm::vec2(first.Get().GetAnchoredPosition()) == glm::vec2(10, 20));
        RectTransformMoveAction empty(std::vector<RectTransformMoveAction::Change>{});
        CHECK_FALSE(empty.Undo());
        CHECK_FALSE(empty.Redo());
    });
}

TEST_CASE("Editor action store RectTransform group move ignores unchanged targets and skips empty drags", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const std::string reason : {"click", "returned", "unchanged_target", "mixed", "missing"})
        {
            CAPTURE(reason);
            RectMoveFixture fixture;
            const auto second = fixture.AddRectTarget(2, {40, 50});
            fixture.Press(2);
            if (reason != "click")
            {
                fixture.Input.Cursor(fixture.Input.First, 354, 204);
                fixture.Update(2);
            }
            if (reason == "returned")
            {
                fixture.Input.Cursor(fixture.Input.First, 330, 220);
                fixture.Update(2);
            }
            if (reason == "unchanged_target") fixture.Native.Registry->Get<ui::RectTransform>(second).GetAnchoredPosition() = {40, 50};
            if (reason == "mixed") fixture.Native.Registry->Del<ui::RectTransform>(second);
            if (reason == "missing")
            {
                fixture.Native.Registry->DestroyEntity(second);
                fixture.Native.World->Refresh();
            }
            fixture.Release();
            if (reason != "unchanged_target")
            {
                CHECK(fixture.Recorded.empty());
                continue;
            }
            REQUIRE(fixture.Recorded.size() == 1);
            fixture.Native.Registry->Del<ui::RectTransform>(second);
            REQUIRE(fixture.Actions->Undo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(10, 20));
            REQUIRE(fixture.Actions->Redo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(34, 36));
        }
    });
}

TEST_CASE("Editor action store RectTransform group resize ignores unchanged targets and skips empty drags", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        for (const std::string reason : {"click", "returned", "unchanged_target", "mixed", "missing"})
        {
            CAPTURE(reason);
            RectMoveFixture fixture;
            const auto second = fixture.AddRectTarget(2, {40, 50});
            fixture.PressResize(2);
            if (reason != "click")
            {
                fixture.Input.Cursor(fixture.Input.First, 354, 204);
                fixture.Body.OnUpdate();
            }
            if (reason == "returned")
            {
                fixture.Input.Cursor(fixture.Input.First, 330, 220);
                fixture.Body.OnUpdate();
            }
            if (reason == "unchanged_target")
            {
                auto& rect = fixture.Native.Registry->Get<ui::RectTransform>(second);
                rect.GetAnchoredPosition() = {40, 50};
                rect.GetSizeDelta() = {100, 80};
            }
            if (reason == "mixed") fixture.Native.Registry->Del<ui::RectTransform>(second);
            if (reason == "missing")
            {
                fixture.Native.Registry->DestroyEntity(second);
                fixture.Native.World->Refresh();
            }
            fixture.Release();
            if (reason != "unchanged_target")
            {
                CHECK(fixture.Recorded.empty());
                continue;
            }
            REQUIRE(fixture.Recorded.size() == 1);
            fixture.Native.Registry->Del<ui::RectTransform>(second);
            REQUIRE(fixture.Actions->Undo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(10, 20));
            CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(200, 160));
            REQUIRE(fixture.Actions->Redo(fixture.Recorded[0]));
            CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(22, 28));
            CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(224, 176));
            CHECK(fixture.Recorded.size() == 1);
        }
    });
}

TEST_CASE("Editor action store RectTransform group resize checks all targets before restoring any field", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        RectMoveFixture fixture;
        const auto second = fixture.AddRectTarget(2, {40, 50});
        ecs::ComponentRef<ui::RectTransform> first(fixture.Native.Registry, fixture.Target);
        ecs::ComponentRef<ui::RectTransform> other(fixture.Native.Registry, second);
        const RectTransformResizeAction::State before{{10, 20}, {200, 160}};
        const RectTransformResizeAction::State after{{22, 28}, {224, 176}};
        RectTransformResizeAction action(std::vector<RectTransformResizeAction::Change>{
            {first, before, after}, {other, {{40, 50}, {100, 80}}, {{52, 58}, {124, 96}}}});
        first.Get().GetAnchoredPosition() = after.AnchoredPosition;
        first.Get().GetSizeDelta() = after.SizeDelta;
        fixture.Native.Registry->Del<ui::RectTransform>(second);
        CHECK_FALSE(action.Undo());
        CHECK(glm::vec2(first.Get().GetAnchoredPosition()) == glm::vec2(after.AnchoredPosition));
        CHECK(glm::vec2(first.Get().GetSizeDelta()) == glm::vec2(after.SizeDelta));
        first.Get().GetAnchoredPosition() = before.AnchoredPosition;
        first.Get().GetSizeDelta() = before.SizeDelta;
        CHECK_FALSE(action.Redo());
        CHECK(glm::vec2(first.Get().GetAnchoredPosition()) == glm::vec2(before.AnchoredPosition));
        CHECK(glm::vec2(first.Get().GetSizeDelta()) == glm::vec2(before.SizeDelta));
        RectTransformResizeAction empty(std::vector<RectTransformResizeAction::Change>{});
        CHECK_FALSE(empty.Undo());
        CHECK_FALSE(empty.Redo());
    });
}

TEST_CASE("Editor action store RectTransform group resize restores individually clamped sizes and pivots", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        RectMoveFixture fixture;
        const auto second = fixture.AddRectTarget(2, {40, 50});
        auto& rect = fixture.Native.Registry->Get<ui::RectTransform>(second);
        rect.REI_SET({{"_pivot", {{"Value", {{"x", {{"Value", 0}}}, {"y", {{"Value", 0}}}}}}}});
        fixture.PressResize(2);
        fixture.Input.Cursor(fixture.Input.First, -670, 1220);
        fixture.Body.OnUpdate();
        CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(4, 4));
        CHECK(glm::vec2(rect.GetSizeDelta()) == glm::vec2(4, 4));
        CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(-88, -58));
        CHECK(glm::vec2(rect.GetAnchoredPosition()) == glm::vec2(40, 50));
        fixture.Release();
        REQUIRE(fixture.Recorded.size() == 1);
        REQUIRE(fixture.Actions->Undo(fixture.Recorded[0]));
        CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(200, 160));
        CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(10, 20));
        CHECK(glm::vec2(rect.GetSizeDelta()) == glm::vec2(100, 80));
        CHECK(glm::vec2(rect.GetAnchoredPosition()) == glm::vec2(40, 50));
        REQUIRE(fixture.Actions->Redo(fixture.Recorded[0]));
        CHECK(glm::vec2(fixture.Rect().GetSizeDelta()) == glm::vec2(4, 4));
        CHECK(glm::vec2(rect.GetSizeDelta()) == glm::vec2(4, 4));
        CHECK(glm::vec2(fixture.Rect().GetAnchoredPosition()) == glm::vec2(-88, -58));
        CHECK(glm::vec2(rect.GetAnchoredPosition()) == glm::vec2(40, 50));
        CHECK(glm::vec2(rect.GetPivot()) == glm::vec2(0, 0));
        CHECK(fixture.Recorded.size() == 1);
    });
}

TEST_CASE("Editor action store RectTransform pivot restores pivot position and size without touching anchors", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        RectMoveFixture fixture;
        ecs::ComponentRef<ui::RectTransform> target(fixture.Native.Registry, fixture.Target);
        const RectTransformPivotAction::State before{{0.5f, 0.5f}, {10, 20}, {200, 160}};
        const RectTransformPivotAction::State after{{0.25f, 0.75f}, {-40, 60}, {220, 180}};
        target.Get().GetPivot() = after.Pivot;
        target.Get().GetAnchoredPosition() = after.AnchoredPosition;
        target.Get().GetSizeDelta() = after.SizeDelta;
        const auto id = fixture.Actions->RecordApplied(std::make_unique<RectTransformPivotAction>(target, before, after));
        const nlohmann::json min = {{"Value", {{"x", {{"Value", 0}}}, {"y", {{"Value", 0}}}}}};
        const nlohmann::json max = {{"Value", {{"x", {{"Value", 1}}}, {"y", {{"Value", 1}}}}}};
        target.Get().REI_SET({{"_anchorMin", min}, {"_anchorMax", max}});
        fixture.TargetTransform().GetLocalScale() = {2, 3, 4};
        fixture.TargetTransform().SetRotation(math::Vector3(10, 20, 30));
        const auto transform = ReadState(fixture.TargetTransform());
        for (const bool undo : {true, false})
        {
            REQUIRE((undo ? fixture.Actions->Undo(id) : fixture.Actions->Redo(id)));
            const auto& expected = undo ? before : after;
            CHECK(glm::vec2(target.Get().GetPivot()) == glm::vec2(expected.Pivot));
            CHECK(glm::vec2(target.Get().GetAnchoredPosition()) == glm::vec2(expected.AnchoredPosition));
            CHECK(glm::vec2(target.Get().GetSizeDelta()) == glm::vec2(expected.SizeDelta));
            CHECK(glm::vec2(target.Get().GetAnchorMin()) == glm::vec2(0, 0));
            CHECK(glm::vec2(target.Get().GetAnchorMax()) == glm::vec2(1, 1));
            CheckState(fixture.TargetTransform(), transform);
        }
        CHECK(fixture.Recorded.size() == 1);
        fixture.Native.Registry->Del<ui::RectTransform>(fixture.Target);
        CHECK_FALSE(fixture.Actions->Undo(id));
        CHECK_FALSE(fixture.Actions->Redo(id));
        CHECK(fixture.Recorded.size() == 1);
    });
}

TEST_CASE("Editor action store RectTransform anchors restore anchors position and size without touching pivot", "[native][editor-actions][isolated]")
{
    Isolated([]
    {
        RectMoveFixture fixture;
        ecs::ComponentRef<ui::RectTransform> target(fixture.Native.Registry, fixture.Target);
        const RectTransformAnchorsAction::State before{{0.5f, 0.5f}, {0.5f, 0.5f}, {10, 20}, {200, 160}};
        const RectTransformAnchorsAction::State after{{0, 0}, {1, 1}, {10, 20}, {-440, -320}};
        target.Get().GetAnchorMin() = after.AnchorMin;
        target.Get().GetAnchorMax() = after.AnchorMax;
        target.Get().GetAnchoredPosition() = after.AnchoredPosition;
        target.Get().GetSizeDelta() = after.SizeDelta;
        const auto id = fixture.Actions->RecordApplied(std::make_unique<RectTransformAnchorsAction>(target, before, after));
        target.Get().GetPivot() = {0.25f, 0.75f};
        fixture.TargetTransform().GetLocalScale() = {2, 3, 4};
        fixture.TargetTransform().SetRotation(math::Vector3(10, 20, 30));
        const auto transform = ReadState(fixture.TargetTransform());
        for (const bool undo : {true, false})
        {
            REQUIRE((undo ? fixture.Actions->Undo(id) : fixture.Actions->Redo(id)));
            const auto& expected = undo ? before : after;
            CHECK(glm::vec2(target.Get().GetAnchorMin()) == glm::vec2(expected.AnchorMin));
            CHECK(glm::vec2(target.Get().GetAnchorMax()) == glm::vec2(expected.AnchorMax));
            CHECK(glm::vec2(target.Get().GetAnchoredPosition()) == glm::vec2(expected.AnchoredPosition));
            CHECK(glm::vec2(target.Get().GetSizeDelta()) == glm::vec2(expected.SizeDelta));
            CHECK(glm::vec2(target.Get().GetPivot()) == glm::vec2(0.25f, 0.75f));
            CheckState(fixture.TargetTransform(), transform);
        }
        CHECK(fixture.Recorded.size() == 1);
        fixture.Native.Registry->Del<ui::RectTransform>(fixture.Target);
        CHECK_FALSE(fixture.Actions->Undo(id));
        CHECK_FALSE(fixture.Actions->Redo(id));
        CHECK(fixture.Recorded.size() == 1);
    });
}
