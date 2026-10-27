#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Editor/Actions/EditorActionStore.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"

using namespace rei;
using namespace rei::editor;
using namespace rei::tests;

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
        const auto id = actions.RecordApplied(std::make_unique<EntityMoveAction>(transform, before, after));
        REQUIRE(id != 0);
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
}
