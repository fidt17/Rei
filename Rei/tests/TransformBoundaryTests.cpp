#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/GeometryTestSupport.h"
#include <set>

using namespace rei;
using namespace rei::math;
using namespace rei::tests;

namespace
{
    bool HasAcyclicParentPath(BehaviourFixture& fixture, ecs::Entity entity)
    {
        std::set<std::pair<i32, u32>> visited;
        while (fixture.Registry->IsAlive(entity))
        {
            if (!visited.insert({entity.Id, entity.Generation}).second) return false;
            if (!fixture.Registry->Has<Transform>(entity)) return true;
            entity = fixture.Registry->Get<Transform>(entity).GetParent();
        }
        return true;
    }

    void SetSerializedParent(BehaviourFixture& fixture, const ecs::Entity child, const i32 parentSceneId)
    {
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(child, PROBE_TRANSFORM, {{"_parent", {{"Value", parentSceneId}}}});
    }
}

TEST_CASE("TRF-03 Zero scale preserves finite collapsed geometry", "[native][transform][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(1);
        const auto child = fixture.Entity(2);
        fixture.Registry->Get<Transform>(parent).GetLocalPosition() = {10, -3, 7};
        fixture.Registry->Get<Transform>(parent).GetLocalScale() = {0, 2, 3};
        fixture.Registry->Get<Transform>(child).GetLocalPosition() = {100, 4, 5};
        fixture.Registry->Get<Transform>(child).SetParent(parent);
        const auto matrix = fixture.Registry->Get<Transform>(child).CalculateWorldModelMatrix();
        CheckVector(Vector3(matrix * glm::vec4(2, 3, 4, 1)), {10, 11, 34});
        CheckVector(fixture.Registry->Get<Transform>(child).GetWorldPosition(), {10, 5, 22});
        CheckVector(fixture.Registry->Get<Transform>(child).GetWorldScale(), {0, 2, 3});
        for (i32 column = 0; column < 4; ++column)
        {
            for (i32 row = 0; row < 4; ++row) CHECK(std::isfinite(matrix[column][row]));
        }
    });
}

TEST_CASE("TRF-03 Singular parent rejects world position without corrupting local value", "[native][transform][coverage][coverage-remaining][isolated][proposed-policy]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(1);
        const auto child = fixture.Entity(2);
        fixture.Registry->Get<Transform>(parent).GetLocalScale() = {0, 2, 3};
        fixture.Registry->Get<Transform>(child).GetLocalPosition() = {1, 2, 3};
        fixture.Registry->Get<Transform>(child).SetParent(parent);
        CHECK_THROWS(fixture.Registry->Get<Transform>(child).SetWorldPosition({4, 6, 9}));
        CheckVector(fixture.Registry->Get<Transform>(child).GetLocalPosition(), {1, 2, 3});
    });
}

TEST_CASE("TRF-03 Zero parent axis permits reachable world scale changes", "[native][transform][coverage][coverage-remaining][isolated][proposed-policy]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(1);
        const auto child = fixture.Entity(2);
        fixture.Registry->Get<Transform>(parent).GetLocalScale() = {0, 2, 3};
        fixture.Registry->Get<Transform>(child).SetParent(parent);
        REQUIRE_NOTHROW(fixture.Registry->Get<Transform>(child).SetWorldScale({0, 6, 9}));
        CheckVector(fixture.Registry->Get<Transform>(child).GetWorldScale(), {0, 6, 9});
    });
}

TEST_CASE("TRF-03 Collapsed parent axis rejects unreachable world scale atomically", "[native][transform][coverage][coverage-remaining][isolated][proposed-policy]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(1);
        const auto child = fixture.Entity(2);
        fixture.Registry->Get<Transform>(parent).GetLocalScale() = {0, 2, 3};
        fixture.Registry->Get<Transform>(child).GetLocalScale() = {1, 2, 3};
        fixture.Registry->Get<Transform>(child).SetParent(parent);
        CHECK_THROWS(fixture.Registry->Get<Transform>(child).SetWorldScale({4, 6, 9}));
        CheckVector(fixture.Registry->Get<Transform>(child).GetLocalScale(), {1, 2, 3});
    });
}

TEST_CASE("TRF-03 Nonuniform parent and rotated child preserve analytic shear matrix", "[native][transform][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(1);
        const auto child = fixture.Entity(2);
        fixture.Registry->Get<Transform>(parent).GetLocalPosition() = {10, -3, 7};
        fixture.Registry->Get<Transform>(parent).GetLocalScale() = {2, 1, 1};
        fixture.Registry->Get<Transform>(child).GetLocalPosition() = {1, 2, 3};
        fixture.Registry->Get<Transform>(child).SetRotation(Vector3(0, 0, 45));
        fixture.Registry->Get<Transform>(child).SetParent(parent);
        const auto matrix = fixture.Registry->Get<Transform>(child).CalculateWorldModelMatrix();
        const f32 rootTwo = std::sqrt(2.0f);
        CheckVector(Vector3(matrix[0]), {rootTwo, rootTwo * 0.5f, 0});
        CheckVector(Vector3(matrix[1]), {-rootTwo, rootTwo * 0.5f, 0});
        CheckVector(Vector3(matrix[3]), {12, -1, 10});
        CheckVector(Vector3(matrix * glm::vec4(2, 3, 0, 1)), {12 - rootTwo, -1 + rootTwo * 2.5f, 10});
        CHECK(glm::dot(glm::vec3(matrix[0]), glm::vec3(matrix[1])) == Catch::Approx(-1.5f).margin(1e-4f));
        CheckVector(fixture.Registry->Get<Transform>(child).GetWorldScale(), {std::sqrt(2.5f), std::sqrt(2.5f), 1});
        // A sheared basis has no exact TRS decomposition. Test actual point
        // mapping; do not pretend component-wise scale/rotation is its oracle.
    });
}

TEST_CASE("TRF-03 Serialized two-entity cycle rejects before parent mutation", "[native][transform][serialization][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(1);
        const auto second = fixture.Entity(2);
        SetSerializedParent(fixture, first, 2);
        REQUIRE(fixture.Registry->Get<Transform>(first).GetParent() == second);
        CHECK_THROWS(SetSerializedParent(fixture, second, 1));
        CHECK(fixture.Registry->Get<Transform>(second).GetParent() == ecs::NULL_ENTITY);
        CHECK(HasAcyclicParentPath(fixture, first));
        CHECK(HasAcyclicParentPath(fixture, second));
    });
}

TEST_CASE("TRF-03 Serialized ancestor cycle rejects across three entities", "[native][transform][serialization][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(1);
        const auto second = fixture.Entity(2);
        const auto third = fixture.Entity(3);
        SetSerializedParent(fixture, second, 1);
        SetSerializedParent(fixture, third, 2);
        CHECK_THROWS(SetSerializedParent(fixture, first, 3));
        CHECK(fixture.Registry->Get<Transform>(first).GetParent() == ecs::NULL_ENTITY);
        CHECK(HasAcyclicParentPath(fixture, third));
    });
}
