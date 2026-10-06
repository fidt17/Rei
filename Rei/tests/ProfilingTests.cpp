#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Common/Profiling/ProfilingService.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Common/Diagnostics/DiagnosticsService.h"
#include "Engine/Services.h"
#include "Common/Logging/Log.h"
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

    const Metric& FindMetric(const Snapshot& snapshot, u64 id)
    {
        for (u32 index = 0; index < snapshot.MetricCount; ++index)
            if (snapshot.Metrics[index].Id == id) return snapshot.Metrics[index];
        throw std::runtime_error("Expected metric is missing.");
    }

    u64 UniformPhaseTotal(const Snapshot& snapshot)
    {
        u64 total = 0;
        for (const auto& marker : {markers::UNIFORMS_LIGHTING, markers::UNIFORMS_CAMERA, markers::UNIFORMS_OBJECT,
            markers::UNIFORMS_MATERIAL, markers::UNIFORMS_OTHER})
            total += FindMetric(snapshot, marker.Id).Value;
        return total;
    }
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

TEST_CASE("Profiler log dumps match native snapshot controls and completed metrics", "[profiling][profiling-log]")
{
    if (!rei::common::logging::Log::GetLogger()) rei::common::logging::Log::Initialize();
    now = 0;
    ProfilingService profiler(1, Clock);
    REQUIRE(profiler.Register(DESCRIPTORS));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    Count(COUNT.Id, 3);
    now = 100;
    profiler.EndFrame();
    profiler.BeginFrame();
    profiler.EndFrame();
    auto logger = rei::common::logging::Log::GetLogger();
    auto dump = [&]
    {
        std::string message;
        const auto handle = logger->NewLogEvent.append([&](const auto& log) { message = log.Message; });
        profiler.RequestLogDump();
        profiler.FlushLogDump();
        logger->NewLogEvent.remove(handle);
        REQUIRE(message.starts_with("Profiling snapshot: "));
        return nlohmann::json::parse(message.substr(std::string("Profiling snapshot: ").size()));
    };
    REQUIRE(dump() == nlohmann::json::parse(ProfilingService::ToJson(profiler.CopySnapshot(), "ok")));
    profiler.SetEnabled(false);
    REQUIRE(dump()["continuousEnabled"] == false);
    REQUIRE(std::string(profiler.RequestCapture(1).Status) == "queued");
    profiler.BeginFrame();
    Count(COUNT.Id, 7);
    now += 100;
    profiler.EndFrame();
    profiler.BeginFrame();
    profiler.EndFrame();
    profiler.SetEnabled(true);
    REQUIRE(dump() == nlohmann::json::parse(ProfilingService::ToJson(profiler.CopySnapshot(SnapshotView::LastCapture), "ok")));
    profiler.Shutdown();
}

TEST_CASE("Uniform upload phases restore nested attribution without timer reads", "[profiling][render-profiling]")
{
    now = 10;
    clockReads = 0;
    ProfilingService profiler(0, Clock);
    REQUIRE(profiler.Register(markers::ALL));
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    const auto reads = clockReads;
    RecordUniformUpload();
    {
        UniformPhaseScope lighting(UniformPhase::Lighting);
        RecordUniformUpload();
        {
            UniformPhaseScope camera(UniformPhase::Camera);
            RecordUniformUpload();
        }
        RecordUniformUpload();
        try
        {
            UniformPhaseScope material(UniformPhase::Material);
            RecordUniformUpload();
            throw std::runtime_error("expected");
        }
        catch (const std::runtime_error&) { }
        RecordUniformUpload();
    }
    {
        UniformPhaseScope object(UniformPhase::Object);
        RecordUniformUpload();
    }
    RecordUniformUpload();
    CHECK(clockReads == reads);
    now = 100;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.InvalidFrames == 0);
    CHECK(FindMetric(snapshot, markers::UNIFORMS.Id).Value == 8);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_LIGHTING.Id).Value == 3);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_CAMERA.Id).Value == 1);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_OBJECT.Id).Value == 1);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_MATERIAL.Id).Value == 1);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_OTHER.Id).Value == 2);
    CHECK(UniformPhaseTotal(snapshot) == FindMetric(snapshot, markers::UNIFORMS.Id).Value);
    profiler.Shutdown();
}

TEST_CASE("Uniform phases ignore disabled profiling and worker uploads", "[profiling][render-profiling]")
{
    now = 10;
    clockReads = 0;
    ProfilingService profiler(0, Clock);
    REQUIRE(profiler.Register(markers::ALL));
    profiler.BeginFrame();
    {
        UniformPhaseScope ignored(UniformPhase::Lighting);
        for (u32 upload = 0; upload < 5000; ++upload) RecordUniformUpload();
    }
    profiler.EndFrame();
    CHECK(clockReads == 0);
    CHECK(profiler.CopySnapshot().FrameCount == 0);
    profiler.SetEnabled(true);
    profiler.BeginFrame();
    {
        UniformPhaseScope material(UniformPhase::Material);
        std::thread worker([]
        {
            UniformPhaseScope ignored(UniformPhase::Object);
            RecordUniformUpload();
        });
        worker.join();
        RecordUniformUpload();
    }
    RecordUniformUpload();
    now = 100;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot();
    CHECK(snapshot.InvalidFrames == 0);
    CHECK(FindMetric(snapshot, markers::UNIFORMS.Id).Value == 2);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_MATERIAL.Id).Value == 1);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_OTHER.Id).Value == 1);
    CHECK(UniformPhaseTotal(snapshot) == 2);
    profiler.Shutdown();
}

TEST_CASE("Uniform phase from a completed frame cannot overwrite current attribution", "[profiling][render-profiling]")
{
    now = 10;
    ProfilingService profiler(0, Clock);
    REQUIRE(profiler.Register(markers::ALL));
    REQUIRE(std::string(profiler.RequestCapture(2).Status) == "queued");
    profiler.BeginFrame();
    auto stale = std::make_unique<UniformPhaseScope>(UniformPhase::Material);
    RecordUniformUpload();
    now = 20;
    profiler.EndFrame();
    profiler.BeginFrame();
    RecordUniformUpload(); // Frame starts in Other, even while stale object exists.
    {
        UniformPhaseScope lighting(UniformPhase::Lighting);
        stale.reset();
        RecordUniformUpload();
    }
    RecordUniformUpload();
    now = 30;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot(SnapshotView::LastCapture);
    CHECK(snapshot.State == CaptureState::Complete);
    CHECK(snapshot.SampleFrames == 2);
    CHECK(snapshot.InvalidFrames == 0);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_MATERIAL.Id).Value == 1);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_LIGHTING.Id).Value == 1);
    CHECK(FindMetric(snapshot, markers::UNIFORMS_OTHER.Id).Value == 2);
    CHECK(UniformPhaseTotal(snapshot) == 4);
    profiler.Shutdown();
}

TEST_CASE("Registered render scopes preserve exclusive sums and fit snapshot limits", "[profiling][render-profiling]")
{
    now = 0;
    ProfilingService profiler(0, Clock);
    REQUIRE(profiler.Register(markers::ALL));
    REQUIRE(profiler.Register(DESCRIPTORS)); // Project DLL markers retain registry capacity.
    REQUIRE(std::string(profiler.RequestCapture(1).Status) == "queued");
    profiler.BeginFrame();
    now = 10;
    {
        Scope scene(markers::SCENE.Id);
        now = 20;
        {
            Scope geometry(markers::GEOMETRY.Id);
            now = 25;
            { Scope lighting(markers::LIGHTING_APPLY.Id); now = 30; }
            { Scope object(markers::OBJECT_DATA.Id); now = 40; }
            { Scope material(markers::MATERIAL.Id); now = 55; }
            { Scope mesh(markers::MESH_SUBMIT.Id); now = 80; }
            now = 90;
        }
        { Scope outline(markers::OUTLINE_PASS.Id); now = 100; }
        { Scope output(markers::OUTPUT.Id); now = 110; }
        now = 120;
    }
    now = 200;
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot(SnapshotView::LastCapture);
    CHECK(snapshot.InvalidFrames == 0);
    CHECK(snapshot.DroppedScopes == 0);
    CHECK(FindMetric(snapshot, markers::SCENE.Id).InclusiveNs == 110);
    CHECK(FindMetric(snapshot, markers::SCENE.Id).ExclusiveNs == 20);
    CHECK(FindMetric(snapshot, markers::GEOMETRY.Id).InclusiveNs == 70);
    CHECK(FindMetric(snapshot, markers::GEOMETRY.Id).ExclusiveNs == 15);
    u64 exclusive = 0;
    for (u32 index = 0; index < snapshot.MetricCount; ++index) exclusive += snapshot.Metrics[index].ExclusiveNs;
    CHECK(exclusive == 110);
    const auto json = nlohmann::json::parse(ProfilingService::ToJson(snapshot, "ok"));
    CHECK(json.at("completeData") == true);
    CHECK(json.at("truncated") == false);
    CHECK(json.at("metrics").size() == markers::ALL.size() + DESCRIPTORS.size());
    const auto limited = nlohmann::json::parse(ProfilingService::ToJson(snapshot, "ok", 2));
    CHECK(limited.at("truncated") == true);
    CHECK(limited.at("completeData") == true);
    CHECK(limited.at("metrics").size() == 2);
    profiler.Shutdown();
}
