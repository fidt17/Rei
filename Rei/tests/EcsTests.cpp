#include "pch.h"
#include "catch_amalgamated.hpp"
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
    auto& e = w.NewEntity();
    w.GetComponent<C1>(e) = C1{7};

    REQUIRE(w.HasComponent<C1>(e));
    REQUIRE(w.GetComponent<C1>(e).Value == 7);
}

TEST_CASE("Add Multiple Components")
{
    World w;
    auto& e = w.NewEntity();
    w.GetComponent<C1>(e) = C1{7};
    w.GetComponent<C2>(e) = C2{14};
    
    REQUIRE(w.GetComponent<C1>(e).Value == 7);
    REQUIRE(w.GetComponent<C2>(e).Value == 14);
}
