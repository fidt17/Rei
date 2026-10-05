#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Ecs/ComponentRef.h"
#include "Ecs/World.h"
#include "Modules/EntityManagement/EntityManager.h"
#include <cstdlib>

using namespace rei::ecs;
using rei::tests::Isolated;

namespace
{
    struct RefValue { i32 Value = 7; };
}

TEST_CASE("CREF-01 Default reference is null with reserved scene ID", "[native][ecs][component-ref][coverage][coverage-next]")
{
    ComponentRef<RefValue> ref;
    CHECK(ref.IsNull());
    CHECK(ref.SceneEntityId == 0);
}

TEST_CASE("CREF-01 Reference copies access same native component and scene ID", "[native][ecs][component-ref][coverage][coverage-next]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    registry->Get<EntityInfo>(entity).Id = 42;
    ComponentRef<RefValue> ref(registry, entity);
    const auto copy = ref;
    ref = ref;
    REQUIRE_FALSE(copy.IsNull());
    CHECK(copy.SceneEntityId == 42);
    copy.Get().Value = 91;
    CHECK(ref.Get().Value == 91);
    CHECK(&copy.Get() == &registry->Get<RefValue>(entity));
    RefValue& converted = copy;
    CHECK(&converted == &copy.Get());
}

TEST_CASE("CREF-01 Constructing reference adds missing component", "[native][ecs][component-ref][coverage][coverage-next]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    REQUIRE_FALSE(registry->Has<RefValue>(entity));
    ComponentRef<RefValue> ref(registry, entity);
    REQUIRE_FALSE(ref.IsNull());
    CHECK(registry->Has<RefValue>(entity));
    CHECK(ref.SceneEntityId == 0);
    CHECK(ref.Get().Value == 7);
}

TEST_CASE("CREF-01 Removed component makes reference null until explicitly readded", "[native][ecs][component-ref][coverage][coverage-next]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    ComponentRef<RefValue> ref(registry, entity);
    ref.Get().Value = 99;
    registry->Del<RefValue>(entity);
    CHECK(ref.IsNull());
    registry->Get<RefValue>(entity);
    REQUIRE_FALSE(ref.IsNull());
    CHECK(ref.Get().Value == 7);
    CHECK(&ref.Get() == &registry->Get<RefValue>(entity));
}

TEST_CASE("CREF-01 Null Get rejects without recreating removed component", "[native][ecs][component-ref][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        World world;
        const auto registry = world.GetRegistry();
        const auto entity = registry->NewEntity();
        ComponentRef<RefValue> ref(registry, entity);
        registry->Del<RefValue>(entity);
        REQUIRE(ref.IsNull());
        CHECK_THROWS(ref.Get());
        CHECK_FALSE(registry->Has<RefValue>(entity));
    });
}

TEST_CASE("CREF-01 Destroyed reference stays null after one slot reuse", "[native][ecs][component-ref][coverage][coverage-next]")
{
    World world;
    const auto registry = world.GetRegistry();
    const auto entity = registry->NewEntity();
    ComponentRef<RefValue> ref(registry, entity);
    registry->DestroyEntity(entity);
    world.Refresh();
    CHECK(ref.IsNull());
    const auto replacement = registry->NewEntity();
    REQUIRE(replacement.Id == entity.Id);
    registry->Get<RefValue>(replacement).Value = 99;
    CHECK(ref.IsNull());
    CHECK_THROWS(ref.Get());
    CHECK(registry->Get<RefValue>(replacement).Value == 99);
}

TEST_CASE("CREF-01 Default Get reports failure without process crash", "[native][ecs][component-ref][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        ComponentRef<RefValue> ref;
        REQUIRE(ref.IsNull());
        CHECK_THROWS(ref.Get());
    });
}

TEST_CASE("CREF-01 Stale implicit conversion reports failure without termination", "[native][ecs][component-ref][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        // Suppress CRT abort UI in this owned child; keep abnormal exit a failure.
        _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
        World world;
        const auto registry = world.GetRegistry();
        const auto entity = registry->NewEntity();
        ComponentRef<RefValue> ref(registry, entity);
        registry->DestroyEntity(entity);
        world.Refresh();
        REQUIRE(ref.IsNull());
        const auto convert = [&]() -> RefValue& { return ref; };
        CHECK_THROWS(convert());
    });
}

TEST_CASE("CREF-02 Resolve binds scene ID in replacement world", "[native][ecs][component-ref][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        const auto first = std::make_shared<World>();
        const auto old = first->GetRegistry()->NewEntity();
        first->GetRegistry()->Get<EntityInfo>(old).Id = 42;
        ComponentRef<RefValue> ref(first->GetRegistry(), old);
        ref.Get().Value = 13;
        const auto second = std::make_shared<World>();
        const auto current = second->GetRegistry()->NewEntity();
        second->GetRegistry()->Get<EntityInfo>(current).Id = 42;
        second->GetRegistry()->Get<RefValue>(current).Value = 91;
        rei::Services::GetInstance()->SetInternalWorld(second);
        rei::Services::GetInstance()->SetEntityManager(std::make_shared<rei::EntityManager>(second));
        second->Refresh();
        CHECK(ref.Get().Value == 13); // Direct reference retains original registry until Resolve.
        ref.Resolve();
        REQUIRE_FALSE(ref.IsNull());
        CHECK(&ref.Get() == &second->GetRegistry()->Get<RefValue>(current));
        CHECK(ref.Get().Value == 91);
    });
}

TEST_CASE("CREF-02 Resolve missing ID or component stays null and read only", "[native][ecs][component-ref][coverage][coverage-next][isolated]")
{
    Isolated([]
    {
        const auto world = std::make_shared<World>();
        const auto registry = world->GetRegistry();
        const auto entity = registry->NewEntity();
        registry->Get<EntityInfo>(entity).Id = 42;
        rei::Services::GetInstance()->SetInternalWorld(world);
        rei::Services::GetInstance()->SetEntityManager(std::make_shared<rei::EntityManager>(world));
        world->Refresh();
        for (const i32 id : {0, 42, 999})
        {
            ComponentRef<RefValue> ref;
            ref.SceneEntityId = id;
            ref.Resolve();
            CHECK(ref.IsNull());
            CHECK_FALSE(registry->Has<RefValue>(entity));
            CHECK(ref.SceneEntityId == id);
        }
    });
}
