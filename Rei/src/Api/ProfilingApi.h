#pragma once
#include <cstring>
#include "Engine/Engine.h"
#include "Engine/Services.h"
#include "Common/Profiling/ProfilingService.h"

namespace rei::api
{
    inline i32 WriteProfilingResponse(const std::string& json, char* buffer, i32 bufferSize)
    {
        const auto required = static_cast<i32>(json.size() + 1);
        if (buffer && bufferSize >= required) std::memcpy(buffer, json.c_str(), required);
        return required;
    }
}

// Called while the managed engine lifecycle lease prevents destruction/DLL unload.
// Reads only a copied published snapshot; never queues a task or enables recording.
REI_EXTERN_API inline i32 GetProfilingSnapshot(const char* view, const char* expectedSessionId, i32 limit, char* buffer, i32 bufferSize)
{
    try
    {
        if (!rei::GetEngine().IsRunning()) return rei::api::WriteProfilingResponse("{\"source\":\"runtime\",\"status\":\"engine_unavailable\"}", buffer, bufferSize);
        if (!view || (std::strcmp(view, "recent") != 0 && std::strcmp(view, "last_capture") != 0) || limit < 1 || limit > rei::profiling::MAX_METRICS) return 0;
        const auto snapshot = rei::GetProfiler().CopySnapshot(std::strcmp(view, "recent") == 0 ? rei::profiling::SnapshotView::Recent : rei::profiling::SnapshotView::LastCapture);
        const char* status = "ok";
        if (expectedSessionId && *expectedSessionId && std::to_string(snapshot.SessionId) != expectedSessionId) status = "session_changed";
        else if (std::strcmp(view, "recent") == 0 && !snapshot.Enabled) status = "disabled";
        else if (snapshot.FrameCount == 0) status = snapshot.Enabled ? "no_samples" : "disabled";
        return rei::api::WriteProfilingResponse(rei::profiling::ProfilingService::ToJson(snapshot, status, limit), buffer, bufferSize);
    }
    catch (...) { return 0; }
}

REI_EXTERN_API inline i32 StartProfilingCapture(i32 frameCount, char* buffer, i32 bufferSize)
{
    try
    {
        if (!rei::GetEngine().IsRunning()) return rei::api::WriteProfilingResponse("{\"source\":\"runtime\",\"status\":\"engine_unavailable\"}", buffer, bufferSize);
        const auto result = rei::GetProfiler().RequestCapture(static_cast<u32>(frameCount));
        const auto json = nlohmann::json({{"source", "runtime"}, {"status", result.Status},
            {"sessionId", std::to_string(result.SessionId)}, {"captureId", std::to_string(result.CaptureId)}, {"requestedFrames", frameCount}}).dump();
        return rei::api::WriteProfilingResponse(json, buffer, bufferSize);
    }
    catch (...) { return 0; }
}

// Explicit Editor Live control. Session guard prevents a closing old window from
// changing a replacement engine after Play/Stop or project DLL reload.
REI_EXTERN_API inline i32 SetProfilingEnabled(i32 enabled, const char* expectedSessionId, char* buffer, i32 bufferSize)
{
    try
    {
        if (!rei::GetEngine().IsRunning()) return rei::api::WriteProfilingResponse("{\"source\":\"runtime\",\"status\":\"engine_unavailable\"}", buffer, bufferSize);
        if (enabled != 0 && enabled != 1) return 0;
        auto& profiler = rei::GetProfiler();
        auto snapshot = profiler.CopySnapshot();
        if (expectedSessionId && *expectedSessionId && std::to_string(snapshot.SessionId) != expectedSessionId)
            return rei::api::WriteProfilingResponse(rei::profiling::ProfilingService::ToJson(snapshot, "session_changed"), buffer, bufferSize);
        profiler.SetEnabled(enabled != 0);
        snapshot = profiler.CopySnapshot();
        return rei::api::WriteProfilingResponse(rei::profiling::ProfilingService::ToJson(snapshot, "ok"), buffer, bufferSize);
    }
    catch (...) { return 0; }
}
