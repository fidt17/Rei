#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Ecs/System.h"
#include "Ecs/World.h"
#include "Modules/Components/ActiveTag.h"
#include <algorithm>
#include <map>
#include <limits>
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

    void CheckEntities(const std::shared_ptr<Filter>& filter, const std::initializer_list<Entity> expected)
    {
        const auto& actual = filter->Entities();
        CHECK(actual.size() == expected.size());
        for (const auto entity : expected)
        {
            CHECK(std::find(actual.begin(), actual.end(), entity) != actual.end());
        }
    }

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
    CHECK(registry->IsDead(deadSlot));
    CHECK_THROWS_AS(registry->Get<ValueComponent>(deadSlot), std::runtime_error);
    CHECK_THROWS_AS(registry->Has<ValueComponent>(deadSlot), std::runtime_error);
    CHECK_THROWS_AS(registry->Del<ValueComponent>(deadSlot), std::runtime_error);
    CHECK_THROWS_AS(registry->GetEntityMask(deadSlot), std::runtime_error);
    CHECK_THROWS_AS(registry->DestroyEntity(deadSlot), std::runtime_error);
    const auto setCount = registry->GetComponentSets().size();
    CHECK_THROWS_AS(registry->Get<OwnedComponent>(deadSlot), std::runtime_error);
    CHECK(registry->GetComponentSets().size() == setCount);

    const auto replacement = registry->NewEntity();
    REQUIRE(replacement.Id == entity.Id);
    REQUIRE(replacement.Generation != 0);
    CHECK(replacement.Generation != entity.Generation);
    REQUIRE(registry->IsAlive(replacement));
    CHECK_FALSE(registry->IsAlive(entity));
    CHECK_FALSE(registry->IsAlive(deadSlot));
    CHECK_FALSE(registry->Has<ValueComponent>(replacement));
    registry->Get<ValueComponent>(replacement).Value = 19;
    world.Refresh();
    CHECK(registry->Get<ValueComponent>(replacement).Value == 19);
}

TEST_CASE("ECS-02 Empty include filters discard destroyed handles", "[native][ecs][coverage]")
{
    World world;
    const auto filter = world.GetFiltersRegistry()->Get<>();
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    world.Refresh();
    REQUIRE(filter->Entities() == std::vector<Entity>{entity});

    const auto inactive = registry->NewEntity();
    registry->Del<rei::ActiveTag>(inactive);
    world.Refresh();
    CheckEntities(filter, {entity, inactive});
    registry->DestroyEntity(entity);
    world.Refresh();
    CheckEntities(filter, {inactive});
    CHECK(registry->IsAlive(inactive));
    world.RefreshAll();
    CheckEntities(filter, {inactive});

    const auto replacement = registry->NewEntity();
    REQUIRE(replacement.Id == entity.Id);
    REQUIRE(replacement.Generation != entity.Generation);
    world.Refresh();
    CheckEntities(filter, {inactive, replacement});
    world.RefreshAll();
    world.RefreshAll();
    CheckEntities(filter, {inactive, replacement});
}

TEST_CASE("ECS-02 Exclude-only filters discard destroyed handles", "[native][ecs][coverage]")
{
    World world;
    const auto filter = world.GetFiltersRegistry()->Get<>(Exclude<ExcludedComponent>());
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    world.Refresh();
    REQUIRE(filter->Entities() == std::vector<Entity>{entity});

    const auto excluded = registry->NewEntity();
    registry->Get<ExcludedComponent>(excluded);
    const auto inactive = registry->NewEntity();
    registry->Del<rei::ActiveTag>(inactive);
    world.Refresh();
    CheckEntities(filter, {entity, inactive});
    registry->DestroyEntity(entity);
    world.Refresh();
    CheckEntities(filter, {inactive});

    registry->Del<ExcludedComponent>(excluded);
    world.Refresh();
    CheckEntities(filter, {inactive, excluded});
    registry->DestroyEntity(excluded);
    world.RefreshAll();
    CheckEntities(filter, {inactive});
    CHECK(registry->IsAlive(excluded)); // Destruction completes only in Refresh.
    world.Refresh();
    CHECK_FALSE(registry->IsAlive(excluded));
    CheckEntities(filter, {inactive});
}

TEST_CASE("ECS-02 New filters skip existing dead slots", "[native][ecs][coverage]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto dead = registry->NewEntity();
    const auto survivor = registry->NewEntity();
    registry->Get<ValueComponent>(survivor).Value = 13;
    registry->DestroyEntity(dead);
    world.Refresh();
    const auto filters = world.GetFiltersRegistry();
    const auto empty = filters->Get<>();
    const auto included = filters->Get<ValueComponent>();
    const auto excluded = filters->Get<>(Exclude<ExcludedComponent>());
    CheckEntities(empty, {survivor});
    CheckEntities(included, {survivor});
    CheckEntities(excluded, {survivor});

    const auto pending = registry->NewEntity();
    registry->DestroyEntity(pending);
    const auto inactive = filters->Get<>(Exclude<rei::ActiveTag>());
    CheckEntities(inactive, {});
    CheckEntities(empty, {survivor});
    CHECK(registry->IsAlive(pending));
    registry->Del<rei::ActiveTag>(survivor);
    world.Refresh();
    CHECK_FALSE(registry->IsAlive(pending));
    CheckEntities(inactive, {survivor});
    world.RefreshAll();
    CheckEntities(empty, {survivor});
    CheckEntities(included, {survivor});
    CheckEntities(excluded, {survivor});
    CheckEntities(inactive, {survivor});
    CHECK(registry->Get<ValueComponent>(survivor).Value == 13);
}

TEST_CASE("ECS-03 Generation reuse does not revive original handles", "[native][ecs][coverage]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto filter = world.GetFiltersRegistry()->Get<>();
    const auto original = registry->NewEntity();
    auto current = original;
    for (u32 reuse = 1; reuse <= 260; ++reuse)
    {
        registry->DestroyEntity(current);
        world.Refresh();
        CheckEntities(filter, {});
        current = registry->NewEntity();
        CAPTURE(reuse, current.Id, current.Generation);
        REQUIRE(current.Generation != 0);
        REQUIRE(registry->IsAlive(current));
        CHECK_FALSE(registry->IsAlive(original));
        CHECK_FALSE(registry->Has<ValueComponent>(current));
        registry->Get<ValueComponent>(current).Value = static_cast<i32>(reuse);
        world.Refresh();
        CheckEntities(filter, {current});
        CHECK(registry->Get<ValueComponent>(current).Value == static_cast<i32>(reuse));
    }
}

TEST_CASE("ECS-03 Exhausted generation retires slot while healthy slots remain reusable", "[native][ecs][coverage]")
{
    constexpr EntityGen MAX_GENERATION = (std::numeric_limits<EntityGen>::max)();
    const auto released = std::make_shared<std::vector<i32>>();
    World world;
    const auto registry = world.GetRegistry();
    const auto original = registry->NewEntity();
    auto exhausted = original;
    for (u32 generation = 2; generation <= MAX_GENERATION; ++generation)
    {
        registry->DestroyEntity(exhausted);
        world.Refresh();
        exhausted = registry->NewEntity();
        REQUIRE(exhausted.Id == original.Id);
        REQUIRE(exhausted.Generation == generation);
    }
    REQUIRE(registry->IsAlive(exhausted));
    registry->Get<OwnedComponent>(exhausted).Resource = MakeToken(released, 43);
    const auto healthy = registry->NewEntity();
    registry->Get<ValueComponent>(healthy).Value = 91;
    const auto filter = world.GetFiltersRegistry()->Get<>();
    CheckEntities(filter, {exhausted, healthy});
    registry->DestroyEntity(exhausted);
    registry->DestroyEntity(healthy);
    REQUIRE(released->empty());
    world.Refresh();
    CHECK(*released == std::vector<i32>{43});
    CheckEntities(filter, {});
    CHECK(registry->GetEntityById(exhausted.Id).Generation == 0);
    CHECK_FALSE(registry->IsAlive(original));
    CHECK_FALSE(registry->IsAlive(exhausted));
    CHECK_THROWS_AS(registry->Get<OwnedComponent>(exhausted), std::runtime_error);

    const auto reused = registry->NewEntity();
    REQUIRE(reused.Id == healthy.Id);
    REQUIRE(reused.Generation == healthy.Generation + 1);
    CHECK_FALSE(registry->Has<ValueComponent>(reused));
    CHECK_FALSE(registry->IsAlive(healthy));
    registry->Get<ValueComponent>(reused).Value = 95;
    const auto fresh = registry->NewEntity();
    CHECK(fresh.Id != exhausted.Id);
    CHECK(fresh.Id != reused.Id);
    CHECK(fresh.Generation == 1);
    world.Refresh();
    CheckEntities(filter, {reused, fresh});
    CHECK(registry->Get<ValueComponent>(reused).Value == 95);
    CHECK_FALSE(registry->IsAlive(exhausted));
    CHECK(*released == std::vector<i32>{43});
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
