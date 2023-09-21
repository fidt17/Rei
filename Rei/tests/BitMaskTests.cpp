#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Ecs/BitMask.h"

using namespace rei::ecs;

TEST_CASE("Bitmask All / Any")
{
    BitMask mask1;
    BitMask mask2;
    
    REQUIRE(mask1.All(mask2));
    REQUIRE(mask2.All(mask1));
    REQUIRE(!mask1.Any(mask2));
    REQUIRE(!mask2.Any(mask1));

    mask1.Set(0);
    REQUIRE(!mask1.All(mask2));
    REQUIRE(mask2.All(mask1));
    REQUIRE(!mask1.Any(mask2));
    REQUIRE(!mask2.Any(mask1));

    mask2.Set(1);
    REQUIRE(!mask1.All(mask2));
    REQUIRE(!mask2.All(mask1));
    REQUIRE(!mask1.Any(mask2));
    REQUIRE(!mask2.Any(mask1));
    
    mask1.Set(1);
    REQUIRE(!mask1.All(mask2));
    REQUIRE(mask2.All(mask1));
    REQUIRE(mask1.Any(mask2));
    REQUIRE(mask2.Any(mask1));

    const u32 maxMaskValue = sizeof(BitMask::mask) * 8;
    mask1.Resize(maxMaskValue);
    mask1.Set(maxMaskValue);
    
    mask2.Resize(maxMaskValue);
    mask2.Remove(maxMaskValue);

    mask1.Set(maxMaskValue);
    mask2.Set(maxMaskValue);
    REQUIRE(!mask1.All(mask2));
    REQUIRE(mask2.All(mask1));
    REQUIRE(mask1.Any(mask2));
    REQUIRE(mask2.Any(mask1));
    
    mask2.Set(0);
    REQUIRE(mask1.All(mask2));
    REQUIRE(mask2.All(mask1));
    REQUIRE(mask1.Any(mask2));
    REQUIRE(mask2.Any(mask1));

    mask1.Clear();
    REQUIRE(mask1.All(mask2));
    REQUIRE(!mask2.All(mask1));
    REQUIRE(!mask1.Any(mask2));
    REQUIRE(!mask2.Any(mask1));
}
