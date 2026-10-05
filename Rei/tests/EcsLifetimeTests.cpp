#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Ecs/System.h"
#include "Ecs/World.h"
#include "Modules/Components/ActiveTag.h"
#include <algorithm>
#include <map>
#include <random>
#include <set>

using namespace rei::ecs;
using rei::tests::Isolated;

namespace
{
    struct ValueComponent { i32 Value = 0; };
    struct ExcludedComponent {};
    struct Token
    {
        std::shared_ptr<std::vector<i32>> Released;
        i32 Id;
        ~Token() { Released->push_back(Id); }
    };
    struct OwnedComponent { std::unique_ptr<Token> Resource; };

    std::unique_ptr<Token> MakeToken(const std::shared_ptr<std::vector<i32>>& released, const i32 id)
    {
        return std::unique_ptr<Token>(new Token{released, id});
    }
}

TEST_CASE("ECS-01 Dead slots cannot be used as live entities", "[native][ecs][coverage]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    registry->Get<ValueComponent>(entity).Value = 7;
    registry->DestroyEntity(entity);
    world.Refresh();
    const auto deadSlot = registry->GetEntityById(entity.Id);
    REQUIRE(deadSlot.Generation == 0);
    CHECK_FALSE(registry->IsAlive(entity));
    CHECK_FALSE(registry->IsAlive(deadSlot));
    CHECK_THROWS(registry->Get<ValueComponent>(deadSlot));
}

TEST_CASE("ECS-02 Empty include filters discard destroyed handles", "[native][ecs][coverage]")
{
    World world;
    const auto filter = world.GetFiltersRegistry()->Get<>();
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    world.Refresh();
    REQUIRE(filter->Entities() == std::vector<Entity>{entity});
    registry->DestroyEntity(entity);
    world.Refresh();
    CHECK(filter->Entities().empty());
    world.RefreshAll();
    CHECK(filter->Entities().empty());
}

TEST_CASE("ECS-02 Exclude-only filters discard destroyed handles", "[native][ecs][coverage]")
{
    World world;
    const auto filter = world.GetFiltersRegistry()->Get<>(Exclude<ExcludedComponent>());
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    world.Refresh();
    REQUIRE(filter->Entities() == std::vector<Entity>{entity});
    registry->DestroyEntity(entity);
    world.Refresh();
    CHECK(filter->Entities().empty());
}

TEST_CASE("ECS-02 New filters skip existing dead slots", "[native][ecs][coverage]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto dead = registry->NewEntity();
    const auto survivor = registry->NewEntity();
    registry->DestroyEntity(dead);
    world.Refresh();
    const auto filter = world.GetFiltersRegistry()->Get<>();
    CHECK(filter->Entities() == std::vector<Entity>{survivor});
}

TEST_CASE("ECS-03 Generation reuse does not revive original handles", "[native][ecs][coverage]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto original = registry->NewEntity();
    auto current = original;
    for (u32 reuse = 1; reuse <= 260; ++reuse)
    {
        registry->DestroyEntity(current);
        world.Refresh();
        current = registry->NewEntity();
        CAPTURE(reuse, current.Generation);
        CHECK_FALSE(registry->IsAlive(original));
    }
}

TEST_CASE("ECS-06 Dense deletion preserves surviving entity values", "[native][ecs][coverage]")
{
    const auto deletedId = GENERATE(0, 1, 2);
    ComponentSet<ValueComponent> components(0);
    bool created = false;
    for (i32 id = 0; id < 3; ++id) components.Get(Entity{id, 1}, created).Value = 100 + id;
    REQUIRE(components.Delete(Entity{deletedId, 1}));
    REQUIRE_FALSE(components.Has(Entity{deletedId, 1}));
    for (i32 id = 0; id < 3; ++id)
    {
        if (id == deletedId) continue;
        CHECK(components.Get(Entity{id, 1}, created).Value == 100 + id);
        CHECK_FALSE(created);
    }
    REQUIRE_FALSE(components.Delete(Entity{deletedId, 1}));
    CHECK(components.Get(Entity{deletedId, 1}, created).Value == 0);
    CHECK(created);
}

TEST_CASE("ECS-07 Component growth and swap moves release each resource once", "[native][ecs][coverage]")
{
    const auto released = std::make_shared<std::vector<i32>>();
    {
        ComponentSet<OwnedComponent> components(0);
        bool created = false;
        for (i32 id = 0; id < 80; ++id) components.Get(Entity{id, 1}, created).Resource = MakeToken(released, id);
        REQUIRE(released->empty());
        REQUIRE(components.Delete(Entity{17, 1}));
        REQUIRE(*released == std::vector<i32>{17});
        for (i32 id = 0; id < 80; ++id)
        {
            if (id == 17) continue;
            REQUIRE(components.Get(Entity{id, 1}, created).Resource->Id == id);
        }
    }
    std::sort(released->begin(), released->end());
    REQUIRE(released->size() == 80);
    for (i32 id = 0; id < 80; ++id) CHECK(released->at(id) == id);
}

TEST_CASE("ECS-07 Deferred destruction releases owned components once", "[native][ecs][coverage]")
{
    const auto released = std::make_shared<std::vector<i32>>();
    World world;
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    registry->Get<OwnedComponent>(entity).Resource = MakeToken(released, 42);
    registry->DestroyEntity(entity);
    registry->DestroyEntity(entity);
    REQUIRE(released->empty());
    world.Refresh();
    REQUIRE(*released == std::vector<i32>{42});
    world.Refresh();
    REQUIRE(released->size() == 1);
}

TEST_CASE("ECS-10 World without systems releases its component storage", "[native][ecs][coverage]")
{
    const auto released = std::make_shared<std::vector<i32>>();
    std::weak_ptr<World> weakWorld;
    {
        const auto world = std::make_shared<World>();
        weakWorld = world;
        const auto registry = world->GetRegistry();
        registry->Get<OwnedComponent>(registry->NewEntity()).Resource = MakeToken(released, 9);
    }
    REQUIRE(weakWorld.expired());
    REQUIRE(*released == std::vector<i32>{9});
}

TEST_CASE("ECS-10 World with a system releases its component storage", "[native][ecs][coverage][isolated]")
{
    Isolated([]
    {
        const auto released = std::make_shared<std::vector<i32>>();
        std::weak_ptr<World> weakWorld;
        {
            const auto world = std::make_shared<World>();
            weakWorld = world;
            const auto registry = world->GetRegistry();
            registry->Get<OwnedComponent>(registry->NewEntity()).Resource = MakeToken(released, 9);
            world->AddSystem(std::function<void()>{[] {}});
        }
        CHECK(weakWorld.expired());
        CHECK(*released == std::vector<i32>{9});
    });
}

TEST_CASE("ECS-08 Reused entities do not inherit destroyed components", "[native][ecs][coverage]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto old = registry->NewEntity();
    registry->Get<ValueComponent>(old).Value = 42;
    const auto filter = world.GetFiltersRegistry()->Get<ValueComponent>();
    world.Refresh();
    registry->DestroyEntity(old);
    world.Refresh();
    const auto replacement = registry->NewEntity();
    REQUIRE(replacement.Id == old.Id);
    CHECK_FALSE(registry->Has<ValueComponent>(replacement));
    CHECK(filter->Entities().empty());
    CHECK(registry->Get<ValueComponent>(replacement).Value == 0);
    world.Refresh();
    CHECK(filter->Entities() == std::vector<Entity>{replacement});
}

TEST_CASE("ECS-09 Null handles reject component access", "[native][ecs][coverage]")
{
    World world;
    const auto registry = world.GetRegistry();
    REQUIRE_FALSE(registry->IsAlive(NULL_ENTITY));
    CHECK_THROWS(registry->Get<ValueComponent>(NULL_ENTITY));
    CHECK_THROWS(registry->Has<ValueComponent>(NULL_ENTITY));
    CHECK_THROWS(registry->Del<ValueComponent>(NULL_ENTITY));
    CHECK_THROWS(registry->GetEntityMask(NULL_ENTITY));
}

TEST_CASE("ECS-11 Seeded registry changes match an independent live entity model", "[native][ecs][coverage]")
{
    struct ExpectedEntity
    {
        Entity Handle;
        bool HasValue;
        bool Excluded;
        i32 Value;
    };

    constexpr u32 SEED = 0xEC5011;
    std::mt19937 random(SEED);
    std::map<i32, ExpectedEntity> model;
    World world;
    const auto registry = world.GetRegistry();
    const auto filter = world.GetFiltersRegistry()->Get<ValueComponent>(Exclude<ExcludedComponent>());
    for (i32 step = 0; step < 1000; ++step)
    {
        const u32 action = model.empty() ? 0 : random() % 5;
        CAPTURE(SEED, step, action);
        if (action == 0)
        {
            const auto entity = registry->NewEntity();
            registry->Get<ValueComponent>(entity).Value = step;
            model.emplace(entity.Id, ExpectedEntity{entity, true, false, step});
        }
        else
        {
            auto selected = model.begin();
            std::advance(selected, random() % model.size());
            auto& expected = selected->second;
            const auto entity = expected.Handle;
            if (action == 1)
            {
                registry->Del<ValueComponent>(entity);
                expected.HasValue = false;
            }
            else if (action == 2)
            {
                registry->Get<ValueComponent>(entity).Value = step;
                expected.HasValue = true;
                expected.Value = step;
            }
            else if (action == 3)
            {
                if (expected.Excluded) registry->Del<ExcludedComponent>(entity);
                else static_cast<void>(registry->Get<ExcludedComponent>(entity));
                expected.Excluded = !expected.Excluded;
            }
            else
            {
                registry->DestroyEntity(entity);
                model.erase(selected);
            }
        }
        world.Refresh();
        std::set<std::pair<i32, u32>> expectedHandles;
        for (const auto& [id, expected] : model)
        {
            CHECK(registry->IsAlive(expected.Handle));
            CHECK(registry->Has<ValueComponent>(expected.Handle) == expected.HasValue);
            CHECK(registry->Has<ExcludedComponent>(expected.Handle) == expected.Excluded);
            if (expected.HasValue) CHECK(registry->Get<ValueComponent>(expected.Handle).Value == expected.Value);
            if (expected.HasValue && !expected.Excluded) expectedHandles.emplace(id, expected.Handle.Generation);
        }
        std::set<std::pair<i32, u32>> actualHandles;
        for (const auto entity : filter->Entities()) actualHandles.emplace(entity.Id, entity.Generation);
        CHECK(actualHandles == expectedHandles);
        CHECK(actualHandles.size() == filter->Entities().size());
    }
}
