#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Editor/Actions/EditorActionStore.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include "Modules/Editor/Actions/EntityScaleAction.h"
#include "Modules/Editor/Actions/EntityRotateAction.h"
#include "Modules/Editor/Actions/EntityOrderAction.h"
#include "Modules/Editor/Actions/EntityReparentAction.h"
#include "Common/Transform/TransformHierarchyUtility.h"
#include "support/InputTestFixture.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/ResetTransformationControlsDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/TransformationControlDragUtilities.h"
#include "rei_behaviours/ui/RectTransform.h"

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
