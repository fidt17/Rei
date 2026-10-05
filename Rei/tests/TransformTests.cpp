#include "pch.h"
#include "support/GeometryTestSupport.h"
#include "rei_behaviours/transformation/Transform.h"
#include "Common/Transform/TransformHierarchyUtility.h"
#include "Ecs/World.h"

using namespace rei;
using namespace rei::ecs;
using namespace rei::math;
using namespace rei::tests;

namespace
{
    // Each test runs in its own bounded child. Restore readable Services binding
    // and explicitly remove Transform self-references before releasing registry.
    class TransformFixture
    {
    public:
        std::shared_ptr<World> WorldState = std::make_shared<World>();
        std::shared_ptr<EcsRegistry> Registry = WorldState->GetRegistry();

        TransformFixture() : _previousWorld(GetInternalWorld())
        {
            Services::GetInstance()->SetInternalWorld(WorldState);
        }

        ~TransformFixture()
        {
            for (const auto entity : _entities)
            {
                if (Registry->IsAlive(entity) && Registry->Has<Transform>(entity)) Registry->Del<Transform>(entity);
            }
            Services::GetInstance()->SetInternalWorld(_previousWorld);
        }

        Entity Add(const i32 sceneId)
        {
            const auto entity = Registry->NewEntity();
            Registry->Get<EntityInfo>(entity) = {sceneId, "test"};
            Registry->Get<Transform>(entity) = Transform(1, entity);
            At(entity).Reset();
            _entities.push_back(entity);
            WorldState->Refresh();
            return entity;
        }

        Transform& At(const Entity entity) const { return Registry->Get<Transform>(entity); }

    private:
        std::shared_ptr<World> _previousWorld;
        std::vector<Entity> _entities;
    };
}

TEST_CASE("TRF-01 Reset restores identity root transform", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto entity = fixture.Add(1);
        auto& transform = fixture.At(entity);
        transform.GetLocalPosition() = {3, 4, 5};
        transform.GetLocalScale() = {2, 3, 4};
        transform.SetRotation(Vector3(20, 30, 40));
        transform.SetChildOrder(9);
        transform.Reset();
        CHECK(transform.GetParent() == NULL_ENTITY);
        CHECK(transform.GetChildOrder() == 0);
        CheckVector(transform.GetWorldPosition(), {});
        CheckVector(transform.GetWorldScale(), {1, 1, 1});
        CheckRotation(transform.GetWorldRotation(), glm::quat(1, 0, 0, 0));
        CheckVector(Vector3(1, 2, 3).Transform(transform.CalculateWorldModelMatrix()), {1, 2, 3});
    });
}

TEST_CASE("TRF-01 Local translation rotation and negative scale transform points", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto entity = fixture.Add(1);
        auto& transform = fixture.At(entity);
        transform.GetLocalPosition() = {10, 20, 30};
        transform.Translate({1, 2, 3});
        transform.GetLocalScale() = {-2, 3, 4};
        transform.SetRotation(Vector3(0, 0, 90));
        CheckVector(Vector3(1, 2, 3).Transform(transform.CalculateModelMatrix()), {5, 20, 45});
        CheckVector(transform.GetLocalPosition(), {11, 22, 33});
        CheckVector(transform.GetLocalScale(), {-2, 3, 4});
        const Transform& readOnly = transform;
        CheckVector(readOnly.GetLocalPosition(), {11, 22, 33});
        CheckVector(readOnly.GetLocalScale(), {-2, 3, 4});
    });
}

TEST_CASE("TRF-01 Local and world rotations apply different axes", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto local = fixture.Add(1);
        const auto world = fixture.Add(2);
        fixture.At(local).SetRotation(Vector3(0, 0, 90));
        fixture.At(world).SetRotation(Vector3(0, 0, 90));
        fixture.At(local).RotateLocal(90, {2, 0, 0});
        fixture.At(world).RotateWorld(90, {2, 0, 0});
        CheckVector(fixture.At(local).GetForward(), {1, 0, 0});
        CheckVector(fixture.At(world).GetForward(), {0, -1, 0});
        CheckVector(fixture.At(local).GetRight(), {0, 1, 0});
        CheckVector(fixture.At(local).GetUp(), {0, 0, 1});
        CHECK(glm::length(fixture.At(local).GetLocalRotation()) == Catch::Approx(1).margin(1e-4f));
    });
}

TEST_CASE("TRF-01 Three-level hierarchy has analytic world position and scale", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto root = fixture.Add(1);
        const auto child = fixture.Add(2);
        const auto leaf = fixture.Add(3);
        fixture.At(root).GetLocalPosition() = {10, 0, 0};
        fixture.At(root).GetLocalScale() = {2, 2, 2};
        fixture.At(root).SetRotation(Vector3(0, 0, 90));
        fixture.At(child).GetLocalPosition() = {1, 0, 0};
        fixture.At(child).GetLocalScale() = {3, 3, 3};
        fixture.At(child).SetParent(root);
        fixture.At(leaf).GetLocalPosition() = {0, 1, 0};
        fixture.At(leaf).SetParent(child);
        CheckVector(fixture.At(leaf).GetWorldPosition(), {4, 2, 0});
        CheckVector(fixture.At(leaf).GetWorldScale(), {6, 6, 6});
        CheckRotation(fixture.At(leaf).GetWorldRotation(), glm::angleAxis(glm::radians(90.0f), glm::vec3(0, 0, 1)));
        CheckVector(Vector3(1, 0, 0).Transform(fixture.At(leaf).CalculateWorldModelMatrix()), {4, 8, 0});
    });
}

TEST_CASE("TRF-01 World setters compute local values under nonsingular parent", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto child = fixture.Add(2);
        fixture.At(parent).GetLocalPosition() = {10, 0, 0};
        fixture.At(parent).GetLocalScale() = {2, 2, 2};
        fixture.At(parent).SetRotation(Vector3(0, 0, 90));
        fixture.At(child).SetParent(parent);
        fixture.At(child).SetWorldPosition({10, 4, 0});
        CheckVector(fixture.At(child).GetLocalPosition(), {2, 0, 0});
        fixture.At(child).SetWorldScale({6, 8, 10});
        CheckVector(fixture.At(child).GetLocalScale(), {3, 4, 5});
        fixture.At(child).SetWorldRotation(glm::quat(1, 0, 0, 0));
        CheckVector(fixture.At(child).GetWorldPosition(), {10, 4, 0});
        CheckVector(fixture.At(child).GetWorldScale(), {6, 8, 10});
        CheckRotation(fixture.At(child).GetWorldRotation(), glm::quat(1, 0, 0, 0));
    });
}

TEST_CASE("TRF-02 Direct reparent and detach preserve local transform", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto child = fixture.Add(2);
        fixture.At(parent).GetLocalPosition() = {10, 0, 0};
        fixture.At(child).GetLocalPosition() = {1, 2, 3};
        fixture.At(child).SetParent(parent);
        CheckVector(fixture.At(child).GetLocalPosition(), {1, 2, 3});
        CheckVector(fixture.At(child).GetWorldPosition(), {11, 2, 3});
        fixture.At(child).SetParent(NULL_ENTITY);
        CheckVector(fixture.At(child).GetWorldPosition(), {1, 2, 3});
    });
}

TEST_CASE("TRF-02 Ordered reparent and detach preserve nonsheared world TRS", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto child = fixture.Add(2);
        fixture.At(parent).GetLocalPosition() = {10, 0, 0};
        fixture.At(parent).GetLocalScale() = {2, 2, 2};
        fixture.At(parent).SetRotation(Vector3(0, 0, 90));
        fixture.At(child).GetLocalPosition() = {2, 3, 4};
        fixture.At(child).GetLocalScale() = {3, 3, 3};
        fixture.At(child).SetRotation(Vector3(0, 0, 30));
        const auto rotation = glm::angleAxis(glm::radians(30.0f), glm::vec3(0, 0, 1));
        for (const auto target : {parent, NULL_ENTITY})
        {
            fixture.At(child).SetParent(target, 0);
            CHECK(fixture.At(child).GetParent() == target);
            CheckVector(fixture.At(child).GetWorldPosition(), {2, 3, 4});
            CheckVector(fixture.At(child).GetWorldScale(), {3, 3, 3});
            CheckRotation(fixture.At(child).GetWorldRotation(), rotation);
        }
    });
}

TEST_CASE("TRF-02 Destroyed parent becomes root without binding reused slot", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto child = fixture.Add(2);
        fixture.At(parent).GetLocalPosition() = {10, 0, 0};
        fixture.At(child).GetLocalPosition() = {1, 2, 3};
        fixture.At(child).SetParent(parent);
        fixture.Registry->DestroyEntity(parent);
        fixture.WorldState->Refresh();
        const auto replacement = fixture.Add(3);
        REQUIRE(replacement.Id == parent.Id);
        fixture.At(replacement).GetLocalPosition() = {100, 0, 0};
        CheckVector(fixture.At(child).GetWorldPosition(), {1, 2, 3});
    });
}

TEST_CASE("TRF-02 Missing parent Transform rejects without adding component", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Registry->NewEntity();
        fixture.Registry->Get<EntityInfo>(parent).Id = 1;
        const auto child = fixture.Add(2);
        fixture.At(child).SetParent(parent);
        CHECK_THROWS(fixture.At(child).CalculateWorldModelMatrix());
        CHECK_THROWS(fixture.At(child).SetWorldPosition({1, 2, 3}));
        CHECK_FALSE(fixture.Registry->Has<Transform>(parent));
    });
}

TEST_CASE("TRF-03 Self-parent request preserves previous parent", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto child = fixture.Add(2);
        fixture.At(child).SetParent(parent);
        fixture.At(child).SetParent(child);
        CHECK(fixture.At(child).GetParent() == parent);
    });
}

TEST_CASE("TRF-03 Ancestor parent request rejects before creating cycle", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto root = fixture.Add(1);
        const auto child = fixture.Add(2);
        const auto leaf = fixture.Add(3);
        fixture.At(child).SetParent(root);
        fixture.At(leaf).SetParent(child);
        fixture.At(root).SetParent(leaf);
        CHECK(fixture.At(root).GetParent() == NULL_ENTITY);
        CHECK(fixture.At(child).GetParent() == root);
        CHECK(fixture.At(leaf).GetParent() == child);
        // No recursive world query after failed rejection: preserve bounded repro.
    });
}

TEST_CASE("HIER-01 Children are sorted scoped and exclude destroyed entities", "[native][transform][hierarchy][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto first = fixture.Add(2);
        const auto second = fixture.Add(3);
        const auto outsider = fixture.Add(4);
        fixture.At(first).SetParent(parent);
        fixture.At(second).SetParent(parent);
        fixture.At(first).SetChildOrder(7);
        fixture.At(second).SetChildOrder(2);
        REQUIRE(fixture.At(parent).GetChildren() == std::vector<Entity>{second, first});
        CHECK(fixture.At(parent).GetMaxChildOrder() == 7);
        CHECK(fixture.At(outsider).GetChildren().empty());
        fixture.Registry->DestroyEntity(second);
        fixture.WorldState->Refresh();
        CHECK(fixture.At(parent).GetChildren() == std::vector<Entity>{first});
    });
}

TEST_CASE("HIER-01 Same-parent moves clamp negative and oversized order", "[native][transform][hierarchy][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto first = fixture.Add(2);
        const auto second = fixture.Add(3);
        const auto third = fixture.Add(4);
        for (const auto child : {first, second, third}) fixture.At(child).SetParent(parent);
        fixture.At(first).SetChildOrder(0);
        fixture.At(second).SetChildOrder(1);
        fixture.At(third).SetChildOrder(2);
        fixture.At(third).SetParent(parent, -1);
        CHECK(fixture.At(parent).GetChildren() == std::vector<Entity>{third, first, second});
        CHECK(fixture.At(third).GetChildOrder() == 0);
        fixture.At(third).SetParent(parent, 100);
        CHECK(fixture.At(parent).GetChildren() == std::vector<Entity>{first, second, third});
        CHECK(fixture.At(third).GetChildOrder() == 2);
        CHECK(fixture.At(first).GetChildOrder() == 0);
        CHECK(fixture.At(second).GetChildOrder() == 1);
    });
}

TEST_CASE("HIER-01 Normalize removes gaps breaks ties by scene ID and is idempotent", "[native][transform][hierarchy][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        TransformFixture fixture;
        const auto parent = fixture.Add(1);
        const auto high = fixture.Add(30);
        const auto low = fixture.Add(10);
        const auto last = fixture.Add(20);
        for (const auto child : {high, low, last}) fixture.At(child).SetParent(parent);
        fixture.At(high).SetChildOrder(7);
        fixture.At(low).SetChildOrder(7);
        fixture.At(last).SetChildOrder(100);
        for (i32 repeat = 0; repeat < 2; ++repeat)
        {
            transform_utility::NormalizeSiblingOrders(parent);
            CHECK(fixture.At(parent).GetChildren() == std::vector<Entity>{low, high, last});
            CHECK(fixture.At(low).GetChildOrder() == 0);
            CHECK(fixture.At(high).GetChildOrder() == 1);
            CHECK(fixture.At(last).GetChildOrder() == 2);
        }
    });
}

TEST_CASE("TRF-01 Fixture releases Transform references and restores Services world", "[native][transform][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        const auto previous = GetInternalWorld();
        std::weak_ptr<World> world;
        std::weak_ptr<EcsRegistry> registry;
        {
            TransformFixture fixture;
            fixture.Add(1);
            fixture.Add(2);
            world = fixture.WorldState;
            registry = fixture.Registry;
        }
        CHECK(world.expired());
        CHECK(registry.expired());
        CHECK(GetInternalWorld() == previous);
    });
}
