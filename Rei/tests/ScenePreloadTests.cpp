#include "pch.h"
#include "support/AssetTestSupport.h"

using namespace rei;
using namespace rei::assets;
using namespace rei::tests;

TEST_CASE("PRE-01 Empty dependency list completes without worker callbacks", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        CHECK(preloader.Preload({}));
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("PRE-01 Preloader deduplicates IDs and calls distinct loaders on worker threads", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        const auto owner = std::this_thread::get_id();
        std::atomic<i32> first = 0;
        std::atomic<i32> duplicate = 0;
        std::atomic<i32> second = 0;
        std::atomic<i32> wrongThread = 0;
        REQUIRE(preloader.Preload({
            {"a", [&](const auto&) { ++first; if (std::this_thread::get_id() == owner) ++wrongThread; return true; }},
            {"a", [&](const auto&) { ++duplicate; return true; }},
            {"b", [&](const auto&) { ++second; if (std::this_thread::get_id() == owner) ++wrongThread; return true; }}
        }));
        CHECK(first == 1);
        CHECK(duplicate == 0);
        CHECK(second == 1);
        CHECK(wrongThread == 0);
    });
}

TEST_CASE("PRE-01 Worker manager Loads finish before owner-thread deferred PostLoad", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(31)}, {"b", "second", IntegerAsset(47)}}); });
        const auto owner = std::this_thread::get_id();
        i32 completePhase = 0;
        probe.State->OnPostLoad = [&]
        {
            if (fixture.Assets->GetLoadedAssetCount() == 2) ++completePhase;
        };
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        REQUIRE(preloader.Preload({
            {"a", [&](const auto&) { return fixture.Assets->GetById<AssetProbe>("a").IsLoaded(); }},
            {"b", [&](const auto&) { return fixture.Assets->GetById<AssetProbe>("b").IsLoaded(); }}
        }));
        CHECK(probe.State->Constructed == 2);
        CHECK(probe.State->PostLoads == 2);
        CHECK(completePhase == 2);
        CHECK(probe.State->PostLoadThreads == std::vector<std::thread::id>{owner, owner});
        auto a = fixture.Assets->GetById<AssetProbe>("a");
        auto b = fixture.Assets->GetById<AssetProbe>("b");
        REQUIRE(a.IsLoaded());
        REQUIRE(b.IsLoaded());
        CHECK(a.Get()->Value == 31);
        CHECK(b.Get()->Value == 47);
        fixture.Assets->Release(a);
        fixture.Assets->Release(b);
        CHECK(a.IsLoaded()); // Public Load in callback acquired each payload once.
        CHECK(b.IsLoaded());
        fixture.Assets->Release(a);
        fixture.Assets->Release(b);
        CHECK_FALSE(a.IsLoaded());
        CHECK_FALSE(b.IsLoaded());
        CHECK(probe.State->Destroyed == 2);
        probe.State->OnPostLoad = {};
    });
}

TEST_CASE("PRE-02 False and missing dependency report failure while other loaders finish", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        std::atomic<i32> good = 0;
        CHECK_FALSE(preloader.Preload({{"false", [](const auto&) { return false; }}, {"good", [&](const auto&) { ++good; return true; }}}));
        CHECK(good == 1);
        CHECK_FALSE(preloader.Preload({CreateTypedAssetDependency<UnsupportedAsset>("missing")}));
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        CHECK(preloader.Preload({{"good", [](const auto&) { return true; }}}));
    });
}

TEST_CASE("PRE-02 Deferred PostLoad failure reports false and retains Failed native record", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        probe.State->ThrowPostLoad = true;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(31)}}); });
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        CHECK_FALSE(preloader.Preload({{"a", [&](const auto&) { return fixture.Assets->GetById<AssetProbe>("a").IsLoaded(); }}}));
        CHECK(probe.State->PostLoads == 1);
        CHECK(fixture.Assets->InspectLoadedAsset("a").at("status") == "unloaded");
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        fixture.Assets->ReleaseById<AssetProbe>("a");
        CHECK(probe.State->Destroyed == 1);
    });
}

TEST_CASE("PRE-02 Worker exception propagates and subsequent nonempty preload drains queued work", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        CHECK_THROWS_AS(preloader.Preload({{"throws", [&](const auto&) -> bool
        {
            fixture.Assets->CreateAssetWithId<AssetProbe>("a");
            throw std::runtime_error("worker failure");
        }}}), std::runtime_error);
        CHECK(probe.State->PostLoads == 0);
        REQUIRE(preloader.Preload({{"next", [](const auto&) { return true; }}}));
        CHECK(probe.State->PostLoads == 1);
        fixture.Assets->ReleaseById<AssetProbe>("a");
        CHECK(probe.State->Destroyed == 1);
    });
}

TEST_CASE("PRE-02 Empty preload drains pending PostLoad left after worker failure", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        REQUIRE_THROWS(preloader.Preload({{"throws", [&](const auto&) -> bool
        {
            fixture.Assets->CreateAssetWithId<AssetProbe>("a");
            throw std::runtime_error("worker failure");
        }}}));
        REQUIRE(preloader.Preload({}));
        CHECK(probe.State->PostLoads == 1);
    });
}

TEST_CASE("PRE-01 PreloadById reads bytes without implicitly running PostLoad", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(31)}}); });
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        REQUIRE(preloader.PreloadById<AssetProbe>("a"));
        CHECK(probe.State->Constructed == 1);
        CHECK(probe.State->PostLoads == 0); // Direct PreloadById only loads data.
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        CHECK(probe.State->PostLoads == 1);
        CHECK(ref.Get()->Value == 31);
        fixture.Assets->Release(ref);
        CHECK(probe.State->Destroyed == 1);
    });
}
