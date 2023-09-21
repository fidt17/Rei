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
    w.NewEntity();
}

TEST_CASE("Add Single Component")
{
    World w;
    auto e = w.NewEntity();
    const auto r = w.GetRegistry();

    r->GetComponent<C1>(e) = C1 { 7 };

    REQUIRE(r->HasComponent<C1>(e));
    REQUIRE(r->GetComponent<C1>(e).Value == 7);
}

TEST_CASE("Add Multiple Components")
{
    World w;
    auto e = w.NewEntity();
    const auto r = w.GetRegistry();
    r->GetComponent<C1>(e) = C1{7};
    r->GetComponent<C2>(e) = C2{14};
    
    REQUIRE(r->GetComponent<C1>(e).Value == 7);
    REQUIRE(r->GetComponent<C2>(e).Value == 14);
}

TEST_CASE("Delete One Component")
{
    World w;
    auto e = w.NewEntity();
    const auto r = w.GetRegistry();

    r->GetComponent<C1>(e);
    REQUIRE(r->HasComponent<C1>(e));
    r->DeleteComponent<C1>(e);
    REQUIRE(!r->HasComponent<C1>(e));
}

TEST_CASE("Delete Multiple Components")
{
    World w;
    auto e = w.NewEntity();
    const auto r = w.GetRegistry();

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
    const auto f = w.NewFilter();

    w.NewEntity();
    REQUIRE(f->Entities().empty());
}

TEST_CASE("Single Include Filter")
{
    World w;
    const auto ecs = w.GetRegistry();
    const auto f = w.NewFilter()->Include<C1>();

    auto e = w.NewEntity();
    REQUIRE(f->Entities().empty());

    ecs->GetComponent<C1>(e);
    w.Refresh();

    REQUIRE(f->Entities().size() == 1);
}

TEST_CASE("Multiple Include Filter")
{
    World w;
    const auto ecs = w.GetRegistry();
    const auto f = w.NewFilter()->Include<C1, C2, C3>();

    auto e = w.NewEntity();
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
    
    const auto f = w.NewFilter()->Include<C1, C2, C3>()->Exclude<C3>();

    auto e = w.NewEntity();
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