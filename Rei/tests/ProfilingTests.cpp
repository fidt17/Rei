#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Common/Profiling/ProfilingService.h"
#include "Common/Diagnostics/DiagnosticsService.h"
#include "Engine/Services.h"
#include <atomic>
#include <thread>

using namespace rei::profiling;
namespace
{
    u64 now = 0;
    u32 clockReads = 0;
    u64 Clock() { ++clockReads; return now; }
    constexpr auto PARENT = MakeScope("Test.Parent");
    constexpr auto CHILD = MakeScope("Test.Child");
    constexpr auto COUNT = MakeCounter("Test.Count");
    constexpr std::array DESCRIPTORS = {PARENT, CHILD, COUNT};
}

TEST_CASE("Profiler aggregates nested scopes, calls, counters and empty frames", "[profiling]")
{
    now = 0;
    ProfilingService profiler(1, Clock);
    REQUIRE(profiler.Register(DESCRIPTORS));
    REQUIRE(std::string(profiler.RequestCapture(2).Status) == "queued");
    profiler.BeginFrame();
    now = 10;
    {
        Scope parent(PARENT.Id);
        now = 20;
        { Scope child(CHILD.Id); now = 50; }
        now = 100;
    }
    profiler.AddCounter(COUNT.Id, 7);
    REQUIRE(profiler.CopySnapshot(SnapshotView::LastCapture).FrameCount == 0);
    now = 200;
    profiler.EndFrame();
    now = 300;
    profiler.BeginFrame();
    now = 500;
    profiler.EndFrame();
    profiler.BeginFrame();
    profiler.EndFrame();
    const auto snapshot = profiler.CopySnapshot(SnapshotView::LastCapture);
    REQUIRE(snapshot.State == CaptureState::Complete);
    REQUIRE_FALSE(snapshot.Enabled);
    REQUIRE(snapshot.FrameCount == 2);
    REQUIRE(snapshot.SampleFrames == 2);
    REQUIRE(snapshot.DurationNs == 500);
    REQUIRE(snapshot.Metrics[0].Calls == 1);
    REQUIRE(snapshot.Metrics[0].InclusiveNs == 90);
    REQUIRE(snapshot.Metrics[0].ExclusiveNs == 60);
    REQUIRE(snapshot.Metrics[0].MaxCallNs == 90);
    REQUIRE(snapshot.Metrics[1].InclusiveNs == 30);
    REQUIRE(snapshot.Metrics[2].Value == 7);
    const auto json = nlohmann::json::parse(ProfilingService::ToJson(snapshot, "ok"));
    REQUIRE(json["metrics"][0]["averageInclusiveMs"].get<f64>() == Catch::Approx(45e-6));
    REQUIRE(json["metrics"][0]["averageCallMs"].get<f64>() == Catch::Approx(90e-6));
}

TEST_CASE("Profiler RAII unwinds exceptions and recursive scopes", "[profiling]")
{
    now = 0;
    ProfilingService profiler(0, Clock);
    REQUIRE(profiler.Register(DESCRIPTORS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    now = 10;
    try
    {
        Scope parent(PARENT.Id);
        now = 20;
        { Scope recursive(PARENT.Id); now = 50; }
        now = 100;
        throw std::runtime_error("expected");
    }
    catch (const std::runtime_error&) { }
    now = 200;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    REQUIRE(snapshot.InvalidFrames == 0);
    REQUIRE(snapshot.Metrics[0].Calls == 2);
    REQUIRE(snapshot.Metrics[0].InclusiveNs == 120);
    REQUIRE(snapshot.Metrics[0].ExclusiveNs == 90);
    profiler.Shutdown();
}

TEST_CASE("Profiler control is bounded, busy preserves capture, shutdown cancels", "[profiling]")
{
    now = 0;
    clockReads = 0;
    ProfilingService profiler(0, Clock);
    REQUIRE(profiler.Register(DESCRIPTORS));
    profiler.BeginFrame();
    { Scope disabled(PARENT.Id); Count(COUNT.Id); }
    profiler.EndFrame();
    REQUIRE(clockReads == 0);
    REQUIRE(profiler.CopySnapshot().FrameCount == 0);
    REQUIRE(std::string(profiler.RequestCapture(0).Status) == "invalid_frame_count");
    REQUIRE(std::string(profiler.RequestCapture(MAX_CAPTURE_FRAMES + 1).Status) == "invalid_frame_count");
    const auto request = profiler.RequestCapture(3);
    const auto repeat = profiler.RequestCapture(1);
    REQUIRE(std::string(repeat.Status) == "busy");
    REQUIRE(repeat.CaptureId == request.CaptureId);
    profiler.BeginFrame();
    now = 10;
    profiler.EndFrame();
    profiler.BeginFrame();
    profiler.Shutdown();
    const auto snapshot = profiler.CopySnapshot(SnapshotView::LastCapture);
    REQUIRE(snapshot.State == CaptureState::Cancelled);
    REQUIRE(snapshot.FrameCount == 1);
    REQUIRE(snapshot.TargetFrames == 3);
    REQUIRE(std::string(profiler.RequestCapture(1).Status) == "engine_unavailable");
    ProfilingService next;
    REQUIRE(next.CopySnapshot().SessionId != snapshot.SessionId);
    REQUIRE(next.CopySnapshot().FrameCount == 0);
}

TEST_CASE("Profiler rejects collisions atomically and drops overflow frames", "[profiling]")
{
    now = 0;
    ProfilingService profiler(0, Clock);
    constexpr std::array collision = {PARENT, Descriptor{PARENT.Id, "Different", MetricKind::Scope}};
    REQUIRE_FALSE(profiler.Register(collision));
    REQUIRE(profiler.CopySnapshot().MetricCount == 0);
    REQUIRE(profiler.Register(DESCRIPTORS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    std::vector<std::unique_ptr<Scope>> stack;
    for (u32 depth = 0; depth <= MAX_DEPTH; ++depth) stack.push_back(std::make_unique<Scope>(PARENT.Id));
    while (!stack.empty()) stack.pop_back();
    now = 10;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    REQUIRE(snapshot.FrameCount == 1);
    REQUIRE(snapshot.SampleFrames == 0);
    REQUIRE(snapshot.InvalidFrames == 1);
    REQUIRE(snapshot.DroppedScopes == 1);
    REQUIRE(snapshot.Metrics[0].Calls == 0);
    REQUIRE_FALSE(profiler.Register(DESCRIPTORS));
    profiler.Shutdown();
}

TEST_CASE("Profiler snapshots are coherent while reader runs and recent window rolls", "[profiling]")
{
    now = 0;
    ProfilingService profiler(0, Clock);
    REQUIRE(profiler.Register(DESCRIPTORS));
    profiler.SetEnabled(true);
    std::atomic<bool> stop = false;
    std::atomic<bool> coherent = true;
    std::thread reader([&]
    {
        while (!stop.load())
        {
            const auto snapshot = profiler.CopySnapshot();
            if (snapshot.DurationNs != snapshot.FrameCount * 100 || snapshot.Metrics[2].Value != snapshot.SampleFrames) coherent = false;
        }
    });
    for (u32 frame = 0; frame <= RECENT_FRAMES; ++frame)
    {
        profiler.BeginFrame();
        profiler.AddCounter(COUNT.Id);
        now += 100;
        profiler.EndFrame();
    }
    profiler.BeginFrame();
    stop = true;
    reader.join();
    REQUIRE(coherent.load());
    REQUIRE(profiler.CopySnapshot().FrameCount == 1);
    profiler.Shutdown();
}

TEST_CASE("Diagnostics separates complete wall delta from selected sections and swap", "[profiling][diagnostics]")
{
    auto time = std::make_shared<rei::time::TimeService>();
    rei::Services::GetInstance()->SetTime(time);
    time->BeginFrame();
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
    time->BeginFrame();
    rei::common::diagnostics::DiagnosticsService diagnostics;
    diagnostics.SetExecutionTimes({2, 3});
    diagnostics.SetRenderCpuTime(7);
    diagnostics.SetDiagnosticsTime(1);
    diagnostics.SetPresentTime(20);
    diagnostics.Update();
    const auto& snapshot = diagnostics.GetSnapshot();
    REQUIRE(snapshot.CoreTimeMs == 5);
    REQUIRE(snapshot.MeasuredSectionsTimeMs == 13);
    REQUIRE(snapshot.PresentTimeMs == 20);
    REQUIRE(snapshot.FrameTimeMs == Catch::Approx(time->GetDeltaSeconds() * 1000));
    REQUIRE(snapshot.Fps == Catch::Approx(1000 / snapshot.FrameTimeMs));
}
