#pragma once

#include <array>
#include <chrono>
#include <mutex>
#include <span>
#include <string>
#include "Common/Primitives.h"

namespace rei::profiling
{
    constexpr u32 MAX_METRICS = 256;
    constexpr u32 MAX_DEPTH = 64;
    constexpr u32 MAX_CAPTURE_FRAMES = 3600;
    constexpr u32 RECENT_FRAMES = 120;
    constexpr u32 MAX_NAME_LENGTH = 95;

    enum class MetricKind { Scope, Counter };
    enum class CaptureState { Idle, Queued, Recording, Complete, Cancelled };
    enum class SnapshotView { Recent, LastCapture };

    struct Descriptor
    {
        u64 Id;
        const char* Name;
        MetricKind Kind;
    };

    constexpr u64 NameId(const char* name)
    {
        u64 value = 14695981039346656037ull;
        for (; *name; ++name) value = (value ^ static_cast<u8>(*name)) * 1099511628211ull;
        return value;
    }

    constexpr Descriptor MakeScope(const char* name) { return {NameId(name), name, MetricKind::Scope}; }
    constexpr Descriptor MakeCounter(const char* name) { return {NameId(name), name, MetricKind::Counter}; }

    struct Metric
    {
        u64 Id = 0;
        std::array<char, MAX_NAME_LENGTH + 1> Name = {};
        MetricKind Kind = MetricKind::Scope;
        u64 Calls = 0;
        u64 InclusiveNs = 0;
        u64 ExclusiveNs = 0;
        u64 MaxCallNs = 0;
        u64 MaxFrameNs = 0;
        u64 Value = 0;
        u64 MaxFrameValue = 0;
    };

    // Owns names and data. No pointer into a project DLL or mutable collector.
    struct Snapshot
    {
        u64 SessionId = 0;
        i32 EngineMode = 0;
        bool Enabled = false;
        bool ContinuousEnabled = false;
        CaptureState State = CaptureState::Idle;
        u64 CaptureId = 0;
        u32 TargetFrames = 0;
        u32 FrameCount = 0;
        u32 SampleFrames = 0;
        u64 FirstFrame = 0;
        u64 LastFrame = 0;
        u64 DurationNs = 0;
        u64 MaxFrameNs = 0;
        u32 InvalidFrames = 0;
        u64 DroppedScopes = 0;
        u32 MetricCount = 0;
        std::array<Metric, MAX_METRICS> Metrics = {};
    };

    struct CaptureResult
    {
        const char* Status;
        u64 SessionId;
        u64 CaptureId;
    };

    class ProfilingService
    {
        struct StackEntry { u32 Slot; u64 Start; u64 Children; };

    public:
        using Clock = u64 (*)();
        REI_API explicit ProfilingService(i32 engineMode = 0, Clock clock = ReadClock);
        REI_API bool Register(std::span<const Descriptor> descriptors);
        REI_API void SetEnabled(bool enabled);
        REI_API CaptureResult RequestCapture(u32 frameCount);
        REI_API Snapshot CopySnapshot(SnapshotView view = SnapshotView::Recent) const;
        REI_API void RequestLogDump();
        REI_API void FlushLogDump();

        // Engine writer only. Readers/control requests may use other threads.
        REI_API void BeginFrame();
        REI_API void EndFrame();
        REI_API void Shutdown();
        REI_API void AddCounter(u64 id, u64 amount = 1) noexcept;
        REI_API static ProfilingService* Current() noexcept;
        REI_API static u64 ReadClock();
        REI_API static std::string ToJson(const Snapshot& snapshot, const char* status, u32 limit = MAX_METRICS);

    private:
        friend class Scope;
        u32 BeginScope(u64 id) noexcept;
        void EndScope(u32 depth, u64 generation) noexcept;
        u32 Find(u64 id) const noexcept;
        void InitializeSnapshot(Snapshot& snapshot) const;
        Snapshot CopySnapshotLocked(SnapshotView view) const;
        void MergeFrame(Snapshot& snapshot, u64 elapsed);

        Clock _clock;
        u64 _sessionId;
        i32 _engineMode;
        std::array<Metric, MAX_METRICS> _registry = {};
        std::array<u32, MAX_METRICS * 2> _lookup = {};
        u32 _metricCount = 0;
        std::array<Metric, MAX_METRICS> _frame = {};
        std::array<StackEntry, MAX_DEPTH> _stack = {};
        u32 _depth = 0;
        u64 _generation = 0;
        u64 _frameId = 0;
        u64 _frameStart = 0;
        u64 _dropped = 0;
        bool _invalid = false;
        bool _frameOpen = false;
        bool _recording = false;
        bool _pendingFrame = false;
        bool _capturingFrame = false;
        bool _started = false;

        mutable std::mutex _snapshotMutex;
        bool _enabledRequested = false;
        bool _stopped = false;
        bool _logRequested = false;
        u64 _nextCaptureId = 0;
        Snapshot _recent;
        Snapshot _capture;
    };

    static_assert(sizeof(ProfilingService) < 256 * 1024, "Profiler storage must remain bounded below 256 KiB.");

    class Scope final
    {
    public:
        REI_API explicit Scope(u64 id) noexcept;
        REI_API ~Scope() noexcept;
        Scope(const Scope&) = delete;
        Scope& operator=(const Scope&) = delete;

    private:
        ProfilingService* _service = nullptr;
        u32 _depth = 0;
        u64 _generation = 0;
    };

    REI_API void RecordDraw(u64 submittedVertices, u64 triangles) noexcept;
    REI_API void Count(u64 id, u64 amount = 1) noexcept;
}

#define REI_PROFILE_JOIN_IMPL(a, b) a##b
#define REI_PROFILE_JOIN(a, b) REI_PROFILE_JOIN_IMPL(a, b)
#define REI_PROFILE_SCOPE(id) rei::profiling::Scope REI_PROFILE_JOIN(profileScope_, __COUNTER__)(id)
