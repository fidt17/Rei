#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Editor/Actions/EditorActionStore.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include "Modules/Editor/Actions/EntityScaleAction.h"
#include "Modules/Editor/Actions/EntityRotateAction.h"
#include "support/InputTestFixture.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/ResetTransformationControlsDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/TransformationControlDragUtilities.h"
#include "rei_behaviours/ui/RectTransform.h"

using namespace rei;
using namespace rei::editor;
using namespace rei::tests;

namespace
{
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
        for (const std::string reason : {"unchanged", "returned", "scale", "rotation", "rect", "multiple", "inactive"})
        {
            CAPTURE(reason);
            TransformDragFixture fixture;
            fixture.Begin();
            if (reason == "multiple")
            {
                fixture.Control().TargetEntities.push_back(fixture.Native.Entity(2));
                transformation_control_drag::CaptureDragStartTargetStates(fixture.Native.World, fixture.Control());
            }
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
            for (const std::string reason : {"unchanged", "returned", "rect", "multiple", "inactive", "equivalent_rotation"})
            {
                if (mode == Scale && reason == "equivalent_rotation") continue;
                CAPTURE(mode, reason);
                TransformDragFixture fixture;
                fixture.Begin(mode);
                const auto before = fixture.Control().DragStartTargetStates.front();
                if (reason == "multiple")
                {
                    fixture.Control().TargetEntities.push_back(fixture.Native.Entity(2));
                    transformation_control_drag::CaptureDragStartTargetStates(fixture.Native.World, fixture.Control());
                }
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
