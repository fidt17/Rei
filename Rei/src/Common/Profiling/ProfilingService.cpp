#include "pch.h"
#include "ProfilingService.h"
#include "ProfileMarkers.h"
#include "Common/Logging/Log.h"
#include <algorithm>
#include <atomic>
#include <cstring>
#include <optional>

namespace rei::profiling
{
    namespace
    {
        thread_local ProfilingService* current = nullptr;
        std::atomic<u64> lastSession = 0;
        constexpr u32 INVALID_SLOT = MAX_METRICS;
        const char* StateName(CaptureState state)
        {
            switch (state)
            {
                case CaptureState::Queued: return "queued";
                case CaptureState::Recording: return "recording";
                case CaptureState::Complete: return "complete";
                case CaptureState::Cancelled: return "cancelled";
                default: return "idle";
            }
        }
    }

    u64 ProfilingService::ReadClock()
    {
        return static_cast<u64>(std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now().time_since_epoch()).count());
    }

    ProfilingService::ProfilingService(i32 engineMode, Clock clock) : _clock(clock), _engineMode(engineMode)
    {
        auto previous = lastSession.load();
        do { _sessionId = (std::max)(ReadClock(), previous + 1); }
        while (!lastSession.compare_exchange_weak(previous, _sessionId));
        InitializeSnapshot(_recent);
        InitializeSnapshot(_capture);
    }

    u32 ProfilingService::Find(u64 id) const noexcept
    {
        u32 bucket = static_cast<u32>(id % _lookup.size());
        for (u32 probe = 0; probe < _lookup.size(); ++probe)
        {
            const auto slot = _lookup[bucket];
            if (slot == 0) return INVALID_SLOT;
            if (_registry[slot - 1].Id == id) return slot - 1;
            bucket = (bucket + 1) % _lookup.size();
        }
        return INVALID_SLOT;
    }

    bool ProfilingService::Register(std::span<const Descriptor> descriptors)
    {
        std::scoped_lock lock(_snapshotMutex);
        if (_started || _stopped) return false;
        // Validate whole batch before changing the registry.
        u32 additions = 0;
        for (u32 index = 0; index < descriptors.size(); ++index)
        {
            const auto& descriptor = descriptors[index];
            if (!descriptor.Name || !*descriptor.Name || std::strlen(descriptor.Name) > MAX_NAME_LENGTH || descriptor.Id == 0) return false;
            const auto existing = Find(descriptor.Id);
            if (existing != INVALID_SLOT)
            {
                if (std::strcmp(_registry[existing].Name.data(), descriptor.Name) != 0 || _registry[existing].Kind != descriptor.Kind) return false;
                continue;
            }
            bool duplicate = false;
            for (u32 earlier = 0; earlier < index; ++earlier)
            {
                if (descriptors[earlier].Id != descriptor.Id) continue;
                if (std::strcmp(descriptors[earlier].Name, descriptor.Name) != 0 || descriptors[earlier].Kind != descriptor.Kind) return false;
                duplicate = true;
            }
            if (!duplicate) ++additions;
        }
        if (_metricCount + additions > MAX_METRICS) return false;
        for (const auto& descriptor : descriptors)
        {
            if (Find(descriptor.Id) != INVALID_SLOT) continue;
            auto& metric = _registry[_metricCount];
            metric.Id = descriptor.Id;
            metric.Kind = descriptor.Kind;
            std::strcpy(metric.Name.data(), descriptor.Name);
            u32 bucket = static_cast<u32>(descriptor.Id % _lookup.size());
            while (_lookup[bucket] != 0) bucket = (bucket + 1) % _lookup.size();
            _lookup[bucket] = ++_metricCount;
        }
        InitializeSnapshot(_recent);
        InitializeSnapshot(_capture);
        return true;
    }

    void ProfilingService::InitializeSnapshot(Snapshot& snapshot) const
    {
        snapshot = {};
        snapshot.SessionId = _sessionId;
        snapshot.EngineMode = _engineMode;
        snapshot.MetricCount = _metricCount;
        std::copy_n(_registry.begin(), _metricCount, snapshot.Metrics.begin());
    }

    void ProfilingService::SetEnabled(bool enabled)
    {
        std::scoped_lock lock(_snapshotMutex);
        if (!_stopped) _enabledRequested = enabled;
    }

    CaptureResult ProfilingService::RequestCapture(u32 frameCount)
    {
        std::scoped_lock lock(_snapshotMutex);
        if (_stopped) return {"engine_unavailable", _sessionId, 0};
        if (frameCount == 0 || frameCount > MAX_CAPTURE_FRAMES) return {"invalid_frame_count", _sessionId, 0};
        if (_capture.State == CaptureState::Queued || _capture.State == CaptureState::Recording) return {"busy", _sessionId, _capture.CaptureId};
        InitializeSnapshot(_capture);
        _capture.CaptureId = ++_nextCaptureId;
        _capture.TargetFrames = frameCount;
        _capture.State = CaptureState::Queued;
        return {"queued", _sessionId, _capture.CaptureId};
    }

    Snapshot ProfilingService::CopySnapshot(SnapshotView view) const
    {
        std::scoped_lock lock(_snapshotMutex);
        return CopySnapshotLocked(view);
    }

    Snapshot ProfilingService::CopySnapshotLocked(SnapshotView view) const
    {
        auto result = view == SnapshotView::Recent ? _recent : _capture;
        result.ContinuousEnabled = !_stopped && _enabledRequested;
        result.Enabled = !_stopped && (_enabledRequested || _capture.State == CaptureState::Queued || _capture.State == CaptureState::Recording);
        return result;
    }

    void ProfilingService::BeginFrame()
    {
        // Publish the preceding completed frame at the next start boundary. Its wall
        // interval includes collector publication, queued work and inter-frame waits.
        const auto boundary = _pendingFrame ? _clock() : 0;
        std::scoped_lock lock(_snapshotMutex);
        _started = true;
        if (_stopped) return;
        if (_pendingFrame)
        {
            _invalid |= boundary < _frameStart;
            const auto elapsed = boundary >= _frameStart ? boundary - _frameStart : 0;
            if (_recent.FrameCount >= RECENT_FRAMES) InitializeSnapshot(_recent);
            MergeFrame(_recent, elapsed);
            if (_capturingFrame && _capture.State == CaptureState::Recording)
            {
                MergeFrame(_capture, elapsed);
                if (_capture.FrameCount == _capture.TargetFrames) _capture.State = CaptureState::Complete;
            }
            _pendingFrame = false;
        }
        ++_frameId;
        ++_generation;
        _depth = 0;
        _invalid = false;
        _dropped = 0;
        if (_capture.State == CaptureState::Queued) _capture.State = CaptureState::Recording;
        _capturingFrame = _capture.State == CaptureState::Recording;
        _recording = _enabledRequested || _capturingFrame;
        _frameOpen = true;
        current = _recording ? this : nullptr;
        if (!_recording) return;
        _frameStart = boundary ? boundary : _clock();
        std::fill_n(_frame.begin(), _metricCount, Metric{});
    }

    u32 ProfilingService::BeginScope(u64 id) noexcept
    {
        const auto depth = _depth++;
        if (depth >= MAX_DEPTH)
        {
            ++_dropped;
            _invalid = true;
            return depth;
        }
        const auto slot = Find(id);
        if (slot == INVALID_SLOT || _registry[slot].Kind != MetricKind::Scope)
        {
            ++_dropped;
            _invalid = true;
        }
        _stack[depth] = {slot, _clock(), 0};
        return depth;
    }

    void ProfilingService::EndScope(u32 depth, u64 generation) noexcept
    {
        if (current != this || !_frameOpen || generation != _generation) return;
        if (_depth != depth + 1)
        {
            _invalid = true;
            return;
        }
        --_depth;
        if (depth >= MAX_DEPTH) return;
        const auto& entry = _stack[depth];
        const auto now = _clock();
        if (now < entry.Start || now - entry.Start < entry.Children)
        {
            _invalid = true;
            return;
        }
        const auto elapsed = now - entry.Start;
        if (depth > 0) _stack[depth - 1].Children += elapsed;
        if (entry.Slot == INVALID_SLOT) return;
        auto& metric = _frame[entry.Slot];
        ++metric.Calls;
        metric.InclusiveNs += elapsed;
        metric.ExclusiveNs += elapsed - entry.Children;
        metric.MaxCallNs = (std::max)(metric.MaxCallNs, elapsed);
    }

    void ProfilingService::AddCounter(u64 id, u64 amount) noexcept
    {
        if (current != this) return;
        const auto slot = Find(id);
        if (slot == INVALID_SLOT || _registry[slot].Kind != MetricKind::Counter)
        {
            _invalid = true;
            ++_dropped;
            return;
        }
        _frame[slot].Value += amount;
    }

    void ProfilingService::MergeFrame(Snapshot& snapshot, u64 elapsed)
    {
        if (snapshot.FrameCount == 0) snapshot.FirstFrame = _frameId;
        snapshot.LastFrame = _frameId;
        ++snapshot.FrameCount;
        snapshot.DurationNs += elapsed;
        snapshot.MaxFrameNs = (std::max)(snapshot.MaxFrameNs, elapsed);
        snapshot.DroppedScopes += _dropped;
        if (_invalid)
        {
            ++snapshot.InvalidFrames;
            return;
        }
        ++snapshot.SampleFrames;
        for (u32 slot = 0; slot < _metricCount; ++slot)
        {
            auto& total = snapshot.Metrics[slot];
            const auto& frame = _frame[slot];
            total.Calls += frame.Calls;
            total.InclusiveNs += frame.InclusiveNs;
            total.ExclusiveNs += frame.ExclusiveNs;
            total.MaxCallNs = (std::max)(total.MaxCallNs, frame.MaxCallNs);
            total.MaxFrameNs = (std::max)(total.MaxFrameNs, frame.InclusiveNs);
            total.Value += frame.Value;
            total.MaxFrameValue = (std::max)(total.MaxFrameValue, frame.Value);
        }
    }

    void ProfilingService::EndFrame()
    {
        if (!_frameOpen) return;
        _frameOpen = false;
        current = nullptr;
        if (!_recording) return;
        _invalid |= _depth != 0;
        _pendingFrame = true;
    }

    void ProfilingService::Shutdown()
    {
        current = nullptr;
        _frameOpen = false;
        _pendingFrame = false;
        _recording = false;
        std::scoped_lock lock(_snapshotMutex);
        _stopped = true;
        _enabledRequested = false;
        if (_capture.State == CaptureState::Queued || _capture.State == CaptureState::Recording) _capture.State = CaptureState::Cancelled;
    }

    ProfilingService* ProfilingService::Current() noexcept { return current; }

    Scope::Scope(u64 id) noexcept : _service(ProfilingService::Current())
    {
        if (!_service) return;
        _generation = _service->_generation;
        _depth = _service->BeginScope(id);
    }

    Scope::~Scope() noexcept
    {
        if (_service) _service->EndScope(_depth, _generation);
    }

    void Count(u64 id, u64 amount) noexcept
    {
        if (auto* service = ProfilingService::Current()) service->AddCounter(id, amount);
    }

    void RecordDraw(u64 submittedVertices, u64 triangles) noexcept
    {
        auto* service = ProfilingService::Current();
        if (!service) return;
        service->AddCounter(markers::DRAW_CALLS.Id);
        service->AddCounter(markers::VERTICES.Id, submittedVertices);
        service->AddCounter(markers::TRIANGLES.Id, triangles);
    }

    std::string ProfilingService::ToJson(const Snapshot& snapshot, const char* status, u32 limit)
    {
        auto metrics = nlohmann::json::array();
        const auto count = (std::min)(snapshot.MetricCount, limit);
        for (u32 slot = 0; slot < count; ++slot)
        {
            const auto& metric = snapshot.Metrics[slot];
            const auto frames = snapshot.SampleFrames;
            metrics.push_back({{"id", std::to_string(metric.Id)}, {"name", metric.Name.data()},
                {"kind", metric.Kind == MetricKind::Scope ? "scope" : "counter"}, {"calls", metric.Calls},
                {"inclusiveMs", metric.InclusiveNs / 1e6}, {"exclusiveMs", metric.ExclusiveNs / 1e6},
                {"averageInclusiveMs", frames ? metric.InclusiveNs / 1e6 / frames : 0},
                {"averageExclusiveMs", frames ? metric.ExclusiveNs / 1e6 / frames : 0},
                {"averageCallMs", metric.Calls ? metric.InclusiveNs / 1e6 / metric.Calls : 0},
                {"maxCallMs", metric.MaxCallNs / 1e6}, {"maxFrameMs", metric.MaxFrameNs / 1e6},
                {"value", metric.Value}, {"averageValue", frames ? static_cast<f64>(metric.Value) / frames : 0},
                {"maxFrameValue", metric.MaxFrameValue}});
        }
        return nlohmann::json({{"source", "runtime"}, {"status", status}, {"sessionId", std::to_string(snapshot.SessionId)},
            {"engineMode", snapshot.EngineMode == 1 ? "PlayMode" : "EditorMode"}, {"enabled", snapshot.Enabled}, {"continuousEnabled", snapshot.ContinuousEnabled},
            {"captureId", std::to_string(snapshot.CaptureId)}, {"captureState", StateName(snapshot.State)},
            {"targetFrames", snapshot.TargetFrames}, {"completedFrames", snapshot.FrameCount}, {"sampleFrames", snapshot.SampleFrames},
            {"firstFrame", snapshot.FirstFrame}, {"lastFrame", snapshot.LastFrame}, {"durationMs", snapshot.DurationNs / 1e6},
            {"averageFrameMs", snapshot.FrameCount ? snapshot.DurationNs / 1e6 / snapshot.FrameCount : 0},
            {"maxFrameMs", snapshot.MaxFrameNs / 1e6}, {"fps", snapshot.DurationNs ? snapshot.FrameCount * 1e9 / snapshot.DurationNs : 0},
            {"invalidFrames", snapshot.InvalidFrames}, {"droppedScopes", snapshot.DroppedScopes},
            {"completeData", snapshot.InvalidFrames == 0}, {"metricCount", snapshot.MetricCount}, {"truncated", count < snapshot.MetricCount},
            {"drawCoverage", "Rei submissions; excludes ImGui and direct project GL calls; submitted vertices are index references"},
            {"metrics", std::move(metrics)}}).dump();
    }

    void ProfilingService::RequestLogDump()
    {
        std::scoped_lock lock(_snapshotMutex);
        _logRequested = true;
    }

    void ProfilingService::FlushLogDump()
    {
        std::optional<Snapshot> snapshot;
        {
            std::scoped_lock lock(_snapshotMutex);
            if (!_logRequested || _capture.State == CaptureState::Queued || _capture.State == CaptureState::Recording) return;
            _logRequested = false;
            snapshot.emplace(CopySnapshotLocked(_capture.CaptureId ? SnapshotView::LastCapture : SnapshotView::Recent));
        }
        LOG("Profiling snapshot: {}", ToJson(*snapshot, snapshot->FrameCount ? "ok" : "no_samples"))
    }
}
