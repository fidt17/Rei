#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Ecs/FiltersRegistry.h"
#include "Ecs/World.h"

using namespace rei::ecs;

struct C1
{
    int Value;
};

struct C2
{
    int Value;
};

struct C3
{
    int Value;
};

TEST_CASE("Create Entity")
{
    auto w = World();
    w.GetRegistry()->NewEntity();
}

TEST_CASE("Add Single Component")
{
    World w;
    const auto r = w.GetRegistry();
    auto e = r->NewEntity();

    r->GetComponent<C1>(e) = C1{7};

    REQUIRE(r->HasComponent<C1>(e));
    REQUIRE(r->GetComponent<C1>(e).Value == 7);
}

TEST_CASE("Add Multiple Components")
{
    World w;
    const auto r = w.GetRegistry();
    auto e = r->NewEntity();
    r->GetComponent<C1>(e) = C1{7};
    r->GetComponent<C2>(e) = C2{14};

    REQUIRE(r->GetComponent<C1>(e).Value == 7);
    REQUIRE(r->GetComponent<C2>(e).Value == 14);
}

TEST_CASE("Delete One Component")
{
    World w;
    const auto r = w.GetRegistry();
    auto e = r->NewEntity();

    r->GetComponent<C1>(e);
    REQUIRE(r->HasComponent<C1>(e));
    r->DeleteComponent<C1>(e);
    REQUIRE(!r->HasComponent<C1>(e));
}

TEST_CASE("Delete Multiple Components")
{
    World w;
    const auto r = w.GetRegistry();
    auto e = r->NewEntity();

    r->GetComponent<C1>(e);
    r->GetComponent<C2>(e);
    REQUIRE(r->HasComponent<C1>(e));
    REQUIRE(r->HasComponent<C2>(e));

    r->DeleteComponent<C1>(e);
    REQUIRE(!r->HasComponent<C1>(e));
    REQUIRE(r->HasComponent<C2>(e));

    r->GetComponent<C1>(e);
    r->DeleteComponent<C2>(e);
    REQUIRE(r->HasComponent<C1>(e));
    REQUIRE(!r->HasComponent<C2>(e));
}

TEST_CASE("Empty Filter")
{
    World w;
    const auto ecs = w.GetRegistry();
    const auto f = w.GetFiltersRegistry()->NewFilter();

    ecs->NewEntity();
    REQUIRE(f->Entities().empty());
}

TEST_CASE("Single Include Filter")
{
    World w;
    const auto ecs = w.GetRegistry();
    const auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1>();

    auto e = ecs->NewEntity();
    REQUIRE(f->Entities().empty());

    ecs->GetComponent<C1>(e);
    w.Refresh();

    REQUIRE(f->Entities().size() == 1);
}

TEST_CASE("Multiple Include Filter")
{
    World w;
    const auto ecs = w.GetRegistry();
    const auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1, C2, C3>();

    auto e = ecs->NewEntity();
    REQUIRE(f->Entities().empty());

    ecs->GetComponent<C1>(e);
    w.Refresh();

    REQUIRE(f->Entities().empty());

    ecs->GetComponent<C2>(e);
    w.Refresh();

    REQUIRE(f->Entities().empty());

    ecs->GetComponent<C3>(e);
    w.Refresh();

    REQUIRE(f->Entities().size() == 1);
}

TEST_CASE("Multiple Include & Exclude Filter")
{
    World w;
    const auto ecs = w.GetRegistry();

    const auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1, C2, C3>()->Exclude<C3>();

    auto e = ecs->NewEntity();
    REQUIRE(f->Entities().empty());

    ecs->GetComponent<C1>(e);
    w.Refresh();

    REQUIRE(f->Entities().empty());

    ecs->GetComponent<C2>(e);
    w.Refresh();

    REQUIRE(f->Entities().size() == 1);

    ecs->GetComponent<C3>(e);
    w.Refresh();

    REQUIRE(f->Entities().empty());
}

TEST_CASE("Destroyed entity gets removed from filters")
{
    World w;
    auto ecs = w.GetRegistry();
    auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1>();

    auto e = ecs->NewEntity();

    ecs->GetComponent<C1>(e);
    w.Refresh();

    REQUIRE(!f->Entities().empty());

    ecs->DestroyEntity(e);
    w.Refresh();

    REQUIRE(f->Entities().empty());
}

TEST_CASE("Destroyed entity Id is reserved for future entities")
{
    World w;
    auto ecs = w.GetRegistry();

    auto e0 = ecs->NewEntity();
    ecs->GetComponent<C1>(e0);
    REQUIRE((e0.Id == 0 && e0.Generation == 1));

    auto e1 = ecs->NewEntity();
    REQUIRE((e1.Id == 1 && e1.Generation == 1));

    ecs->DestroyEntity(e0);
    w.Refresh();
    
    REQUIRE(ecs->GetEntityById(e0.Id).Generation == 0);

    auto e2 = ecs->NewEntity();
    REQUIRE((e2.Id == e0.Id && e2.Generation == 2));
    REQUIRE(!ecs->HasComponent<C1>(e2));

    auto e3 = ecs->NewEntity();
    REQUIRE((e3.Id == 2 && e3.Generation == 1));
}

TEST_CASE("Destroyed entity is marked as dead")
{
    World w;
    auto ecs = w.GetRegistry();
    auto e = ecs->NewEntity();

    REQUIRE(ecs->IsAlive(e));

    ecs->DestroyEntity(e);
    w.Refresh();

    REQUIRE(ecs->IsDead(e));
}

TEST_CASE("Cannot get component on dead entity")
{
    World w;
    auto ecs = w.GetRegistry();
    auto e = ecs->NewEntity();

    ecs->DestroyEntity(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(ecs->GetComponent<C1>(e));
    LOGGER_ENABLE()
}

TEST_CASE("Cannot delete component on dead entity")
{
    World w;
    auto ecs = w.GetRegistry();
    auto e = ecs->NewEntity();
    ecs->DestroyEntity(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(ecs->DeleteComponent<C1>(e));
    LOGGER_ENABLE()
}

TEST_CASE("Cannot check if dead entity has component")
{
    World w;
    auto ecs = w.GetRegistry();
    auto e = ecs->NewEntity();
    ecs->DestroyEntity(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(ecs->HasComponent<C1>(e));
    LOGGER_ENABLE()
}

TEST_CASE("Cannot get mask of dead entity")
{
    World w;
    auto ecs = w.GetRegistry();
    auto e = ecs->NewEntity();

    ecs->DestroyEntity(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(ecs->GetEntityMask(e));
    LOGGER_ENABLE()
}
