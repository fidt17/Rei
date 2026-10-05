#include "pch.h"
#include "support/AssetTestSupport.h"
#include <barrier>

using namespace rei;
using namespace rei::assets;
using namespace rei::tests;

TEST_CASE("PRE-01 Nested suppression restores prior state even after exception", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        REQUIRE_FALSE(AssetPostLoadHandler::IsSuppressedForCurrentThread());
        {
            const AssetPostLoadHandler::ScopedPostLoadSuppression outer(true);
            CHECK(AssetPostLoadHandler::IsSuppressedForCurrentThread());
            CHECK_THROWS([] { const AssetPostLoadHandler::ScopedPostLoadSuppression inner(false); REQUIRE_FALSE(AssetPostLoadHandler::IsSuppressedForCurrentThread()); throw std::runtime_error("scope"); }());
            CHECK(AssetPostLoadHandler::IsSuppressedForCurrentThread());
        }
        CHECK_FALSE(AssetPostLoadHandler::IsSuppressedForCurrentThread());
    });
}

TEST_CASE("PRE-01 Suppression is thread local and new worker starts unsuppressed", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        const AssetPostLoadHandler::ScopedPostLoadSuppression scope(true);
        bool initial = true;
        bool restored = true;
        std::thread worker([&]
        {
            initial = AssetPostLoadHandler::IsSuppressedForCurrentThread();
            { const AssetPostLoadHandler::ScopedPostLoadSuppression inner(true); }
            restored = AssetPostLoadHandler::IsSuppressedForCurrentThread();
        });
        worker.join();
        CHECK_FALSE(initial);
        CHECK_FALSE(restored);
        CHECK(AssetPostLoadHandler::IsSuppressedForCurrentThread());
    });
}

TEST_CASE("PRE-01 Pending queue deduplicates IDs and preserves first callback order", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetPostLoadHandler handler;
        std::vector<i32> calls;
        CHECK(handler.Flush());
        handler.Queue("a", [&] { calls.push_back(1); return true; });
        handler.Queue("a", [&] { calls.push_back(99); return true; });
        handler.Queue("b", [&] { calls.push_back(2); return true; });
        REQUIRE(handler.Flush());
        CHECK(calls == std::vector<i32>{1, 2});
        CHECK(handler.Flush());
        CHECK(calls.size() == 2);
        handler.Queue("a", [&] { calls.push_back(3); return true; });
        CHECK(handler.Flush());
        CHECK(calls == std::vector<i32>{1, 2, 3});
    });
}

TEST_CASE("PRE-01 Callback can queue same ID for subsequent flush without deadlock", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetPostLoadHandler handler;
        i32 first = 0;
        i32 second = 0;
        handler.Queue("a", [&] { ++first; handler.Queue("a", [&] { ++second; return true; }); return true; });
        REQUIRE(handler.Flush());
        CHECK(first == 1);
        CHECK(second == 0);
        REQUIRE(handler.Flush());
        CHECK(second == 1);
        CHECK(handler.Flush());
        CHECK(second == 1);
    });
}

TEST_CASE("PRE-02 False callback does not discard later callbacks or poison next flush", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetPostLoadHandler handler;
        i32 later = 0;
        handler.Queue("false", [] { return false; });
        handler.Queue("later", [&] { ++later; return true; });
        CHECK_FALSE(handler.Flush());
        CHECK(later == 1);
        CHECK(handler.Flush());
    });
}

TEST_CASE("PRE-02 Throwing callback preserves unexecuted work for recovery", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetPostLoadHandler handler;
        i32 later = 0;
        handler.Queue("throws", []() -> bool { throw std::runtime_error("deferred failure"); });
        handler.Queue("later", [&] { ++later; return true; });
        REQUIRE_THROWS_AS(handler.Flush(), std::runtime_error);
        CHECK(later == 0);
        CHECK(handler.Flush());
        CHECK(later == 1);
    });
}

TEST_CASE("PRE-01 Concurrent producers deduplicate pending IDs before owner-thread flush", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetPostLoadHandler handler;
        std::atomic<i32> calls = 0;
        std::barrier start(4);
        std::vector<std::thread> workers;
        for (i32 i = 0; i < 4; ++i) workers.emplace_back([&]
        {
            start.arrive_and_wait();
            for (i32 j = 0; j < 25; ++j) handler.Queue(std::to_string(j), [&] { ++calls; return true; });
        });
        for (auto& worker : workers) worker.join();
        CHECK(calls == 0);
        REQUIRE(handler.Flush());
        CHECK(calls == 25);
    });
}

TEST_CASE("PRE-01 Suppressed manager Loads defer one PostLoad per pending asset ID", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        AssetRef<AssetProbe> ref;
        {
            const AssetPostLoadHandler::ScopedPostLoadSuppression scope(true);
            ref = fixture.Assets->CreateAssetWithId<AssetProbe>("a");
            REQUIRE(fixture.Assets->Load(ref));
            REQUIRE(fixture.Assets->Load(ref));
            CHECK(probe.State->PostLoads == 0);
        }
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        REQUIRE(preloader.Preload({{"flush", [](const auto&) { return true; }}}));
        CHECK(probe.State->PostLoads == 1);
        for (i32 i = 0; i < 3; ++i) fixture.Assets->Release(ref);
        CHECK(probe.State->Destroyed == 1);
    });
}
