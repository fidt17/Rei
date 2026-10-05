#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Ecs/BitMask.h"
#include <array>
#include <set>

using rei::ecs::BitMask;
using rei::tests::Isolated;

namespace
{
    void CheckIndices(const BitMask& mask, const std::set<u64>& expected)
    {
        const auto& words = mask.GetFlags();
        for (size_t word = 0; word < words.size(); ++word)
        {
            u64 value = 0;
            for (const auto index : expected)
            {
                if (index / 64 == word) value |= u64{1} << (index % 64);
            }
            CAPTURE(word, expected.size());
            CHECK(words[word] == value);
        }
        for (const auto index : expected) REQUIRE(index / 64 < words.size());
    }
}

TEST_CASE("ECS-04 Bitmask word boundaries preserve independent bits", "[native][ecs][bitmask][coverage][isolated]")
{
    Isolated([]
    {
        BitMask mask;
        mask.Resize(129);
        std::set<u64> expected;
        for (const auto bit : std::array<u64, 6>{0, 63, 64, 65, 127, 128})
        {
            mask.Set(bit);
            expected.insert(bit);
            CheckIndices(mask, expected);
        }
        for (const auto bit : std::array<u64, 6>{64, 0, 127, 63, 128, 65})
        {
            mask.Remove(bit);
            expected.erase(bit);
            CheckIndices(mask, expected);
        }
    });
}

TEST_CASE("ECS-04 Bitmask growth and clear preserve bit semantics", "[native][ecs][bitmask][coverage][isolated]")
{
    Isolated([]
    {
        BitMask mask;
        mask.Set(63);
        mask.Resize(129);
        CheckIndices(mask, {63});
        mask.Set(128);
        CheckIndices(mask, {63, 128});
        mask.Clear();
        CheckIndices(mask, {});
        mask.Set(65);
        CheckIndices(mask, {65});
    });
}

TEST_CASE("ECS-04 Bitmask subset and overlap compare different words", "[native][ecs][bitmask][coverage][isolated]")
{
    Isolated([]
    {
        BitMask low;
        BitMask high;
        BitMask combined;
        for (auto mask : {&low, &high, &combined}) mask->Resize(129);
        low.Set(0);
        high.Set(64);
        combined.Set(0);
        combined.Set(64);
        combined.Set(128);
        CHECK_FALSE(low.Any(high));
        CHECK(low.All(combined));
        CHECK(high.All(combined));
        CHECK_FALSE(combined.All(low));
        CHECK(combined.Any(high));
        CHECK_FALSE(low == high);
        high.Remove(64);
        CHECK(high.All(combined));
        CHECK_FALSE(high.Any(combined));
    });
}
