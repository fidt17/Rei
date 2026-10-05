#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Common/Profiling/ProfilingService.h"
#include <thread>

using namespace rei::profiling;

namespace
{
    u64 boundaryNow = 0;
    u64 BoundaryClock() { return boundaryNow; }
    constexpr auto BOUNDARY_SCOPE = MakeScope("Coverage.Scope");
    constexpr auto BOUNDARY_COUNTER = MakeCounter("Coverage.Counter");
    constexpr std::array BOUNDARY_METRICS = {BOUNDARY_SCOPE, BOUNDARY_COUNTER};
}

TEST_CASE("Profiler registry accepts 256 metrics with collision probing and rejects overflow atomically", "[native][coverage][coverage-remaining][profiling]")
{
    boundaryNow = 0;
    ProfilingService profiler(0, BoundaryClock);
    std::vector<std::string> names;
    names.reserve(MAX_METRICS);
    for (u32 index = 0; index < MAX_METRICS; ++index) names.push_back("metric-" + std::to_string(index));
    std::vector<Descriptor> descriptors;
    for (u32 index = 0; index < MAX_METRICS; ++index) descriptors.push_back({1 + static_cast<u64>(index) * 512, names[index].c_str(), MetricKind::Counter});
    REQUIRE(profiler.Register(descriptors));
    REQUIRE(profiler.Register(descriptors));
    constexpr std::array extra = {Descriptor{2, "overflow", MetricKind::Counter}};
    CHECK_FALSE(profiler.Register(extra));
    CHECK(profiler.CopySnapshot().MetricCount == MAX_METRICS);
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    for (const auto& descriptor : descriptors) profiler.AddCounter(descriptor.Id, 5);
    boundaryNow = 10;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.SampleFrames == 1);
    CHECK(snapshot.InvalidFrames == 0);
    for (u32 index = 0; index < MAX_METRICS; ++index)
    {
        CHECK(snapshot.Metrics[index].Id == descriptors[index].Id);
        CHECK(snapshot.Metrics[index].Value == 5);
    }
    profiler.Shutdown();
}

TEST_CASE("Profiler malformed descriptor batches leave registry unchanged", "[native][coverage][coverage-remaining][profiling]")
{
    const std::string tooLong(MAX_NAME_LENGTH + 1, 'x');
    for (const Descriptor invalid : {Descriptor{1, nullptr, MetricKind::Scope}, Descriptor{1, "", MetricKind::Scope}, Descriptor{0, "zero-id", MetricKind::Scope}, Descriptor{1, tooLong.c_str(), MetricKind::Scope}})
    {
        ProfilingService profiler;
        const std::array batch = {BOUNDARY_SCOPE, invalid};
        CHECK_FALSE(profiler.Register(batch));
        CHECK(profiler.CopySnapshot().MetricCount == 0);
    }
    ProfilingService profiler;
    const std::string maximum(MAX_NAME_LENGTH, 'x');
    const std::array valid = {Descriptor{1, maximum.c_str(), MetricKind::Scope}};
    REQUIRE(profiler.Register(valid));
    CHECK(std::string(profiler.CopySnapshot().Metrics[0].Name.data()) == maximum);
}

TEST_CASE("Profiler copies descriptor names and JSON escapes content with explicit limit", "[native][coverage][coverage-remaining][profiling]")
{
    std::string name = "scope\"\\\n\t \xD0\xA0\xD0\xB5\xD0\xB9";
    const auto original = name;
    ProfilingService profiler;
    const std::array descriptors = {Descriptor{1, name.c_str(), MetricKind::Scope}, Descriptor{2, "counter", MetricKind::Counter}};
    REQUIRE(profiler.Register(descriptors));
    name.assign(name.size(), 'x');
    const auto snapshot = profiler.CopySnapshot();
    CHECK(std::string(snapshot.Metrics[0].Name.data()) == original);
    const auto encoded = nlohmann::json::parse(ProfilingService::ToJson(snapshot, "ok", 1));
    CHECK(encoded.at("metricCount") == 2);
    CHECK(encoded.at("truncated") == true);
    REQUIRE(encoded.at("metrics").size() == 1);
    CHECK(encoded.at("metrics")[0].at("name") == original);
    const auto empty = nlohmann::json::parse(ProfilingService::ToJson(snapshot, "ok", 0));
    CHECK(empty.at("metrics").empty());
    CHECK(empty.at("truncated") == true);
}

TEST_CASE("Profiler exact depth limit produces valid frame", "[native][coverage][coverage-remaining][profiling]")
{
    boundaryNow = 0;
    ProfilingService profiler(0, BoundaryClock);
    REQUIRE(profiler.Register(BOUNDARY_METRICS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    std::vector<std::unique_ptr<Scope>> scopes;
    for (u32 depth = 0; depth < MAX_DEPTH; ++depth) scopes.push_back(std::make_unique<Scope>(BOUNDARY_SCOPE.Id));
    boundaryNow = 10;
    while (!scopes.empty()) scopes.pop_back();
    boundaryNow = 20;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.InvalidFrames == 0);
    CHECK(snapshot.DroppedScopes == 0);
    CHECK(snapshot.Metrics[0].Calls == MAX_DEPTH);
    CHECK(snapshot.Metrics[0].ExclusiveNs == 10);
    profiler.Shutdown();
}

TEST_CASE("Profiler stale scope from earlier frame cannot corrupt new frame", "[native][coverage][coverage-remaining][profiling]")
{
    boundaryNow = 10;
    ProfilingService profiler(0, BoundaryClock);
    REQUIRE(profiler.Register(BOUNDARY_METRICS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    auto stale = std::make_unique<Scope>(BOUNDARY_SCOPE.Id);
    boundaryNow = 20;
    profiler.EndFrame();
    boundaryNow = 30;
    profiler.BeginFrame();
    boundaryNow = 40;
    stale.reset();
    profiler.AddCounter(BOUNDARY_COUNTER.Id, 7);
    boundaryNow = 50;
    profiler.EndFrame();
    boundaryNow = 60;
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.FrameCount == 2);
    CHECK(snapshot.InvalidFrames == 1);
    CHECK(snapshot.SampleFrames == 1);
    CHECK(snapshot.Metrics[0].Calls == 0);
    CHECK(snapshot.Metrics[1].Value == 7);
    profiler.Shutdown();
}

TEST_CASE("Profiler rejects unknown IDs and metric kind mismatches in whole frame", "[native][coverage][coverage-remaining][profiling]")
{
    boundaryNow = 10;
    ProfilingService profiler(0, BoundaryClock);
    REQUIRE(profiler.Register(BOUNDARY_METRICS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    profiler.AddCounter(BOUNDARY_COUNTER.Id, 7);
    profiler.AddCounter(BOUNDARY_SCOPE.Id, 1);
    { Scope unknown(123456789); }
    boundaryNow = 20;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.InvalidFrames == 1);
    CHECK(snapshot.DroppedScopes == 2);
    CHECK(snapshot.SampleFrames == 0);
    CHECK(snapshot.Metrics[1].Value == 0);
    profiler.Shutdown();
}

TEST_CASE("Profiler worker thread scopes and counters do not contaminate engine writer", "[native][coverage][coverage-remaining][profiling]")
{
    boundaryNow = 10;
    ProfilingService profiler(0, BoundaryClock);
    REQUIRE(profiler.Register(BOUNDARY_METRICS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    std::thread worker([&]
    {
        Scope ignored(BOUNDARY_SCOPE.Id);
        Count(BOUNDARY_COUNTER.Id, 100);
        profiler.AddCounter(BOUNDARY_COUNTER.Id, 100);
    });
    worker.join();
    profiler.AddCounter(BOUNDARY_COUNTER.Id, 3);
    boundaryNow = 20;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.InvalidFrames == 0);
    CHECK(snapshot.Metrics[0].Calls == 0);
    CHECK(snapshot.Metrics[1].Value == 3);
    profiler.Shutdown();
}

TEST_CASE("Profiler backward scope clock invalidates frame without unsigned overflow", "[native][coverage][coverage-remaining][profiling]")
{
    boundaryNow = 100;
    ProfilingService profiler(0, BoundaryClock);
    REQUIRE(profiler.Register(BOUNDARY_METRICS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    {
        Scope scope(BOUNDARY_SCOPE.Id);
        boundaryNow = 50;
    }
    profiler.EndFrame();
    boundaryNow = 200;
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.InvalidFrames == 1);
    CHECK(snapshot.DurationNs == 100);
    CHECK(snapshot.Metrics[0].Calls == 0);
    CHECK(snapshot.Metrics[0].InclusiveNs == 0);
    profiler.Shutdown();
}
