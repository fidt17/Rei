#include "pch.h"
#include "DiagnosticsService.h"

#ifdef _WIN32
#include <windows.h>
#include <psapi.h>
#endif

#include "GLFW/glfw3.h"

namespace rei::common::diagnostics
{
    namespace
    {
        constexpr f32 BYTES_TO_MEGABYTES = 1.0f / (1024.0f * 1024.0f);
    }

    void DiagnosticsService::PushSample(std::array<f32, SAMPLE_COUNT>& samples, i32& sampleIndex, i32& sampleSize, const f32 value) const
    {
        samples[sampleIndex] = value;
        sampleIndex = (sampleIndex + 1) % SAMPLE_COUNT;
        if (sampleSize < SAMPLE_COUNT)
        {
            sampleSize++;
        }
    }

    f32 DiagnosticsService::ComputeAverage(const std::array<f32, SAMPLE_COUNT>& samples, const i32 sampleSize)
    {
        if (sampleSize <= 0) return 0.0f;

        f32 sum = 0.0f;
        for (i32 i = 0; i < sampleSize; i++)
        {
            sum += samples[i];
        }

        return sum / static_cast<f32>(sampleSize);
    }

    void DiagnosticsService::Update()
    {
        const auto coreTimeMs = _rawExecutionTimes.WindowTimeMs + _rawExecutionTimes.UpdateTimeMs;
        const auto activeFrameTimeMs = coreTimeMs + _rawExecutionTimes.RenderCpuTimeMs + _rawExecutionTimes.DiagnosticsTimeMs;

        PushSample(_coreTimeSamples, _coreTimeSampleIndex, _coreTimeSampleSize, coreTimeMs);
        PushSample(_renderTimeSamples, _renderTimeSampleIndex, _renderTimeSampleSize, _rawExecutionTimes.RenderCpuTimeMs);
        PushSample(_presentTimeSamples, _presentTimeSampleIndex, _presentTimeSampleSize, _rawExecutionTimes.PresentTimeMs);
        PushSample(_diagnosticsTimeSamples, _diagnosticsTimeSampleIndex, _diagnosticsTimeSampleSize, _rawExecutionTimes.DiagnosticsTimeMs);

        _snapshot.CoreTimeMs = ComputeAverage(_coreTimeSamples, _coreTimeSampleSize);
        _snapshot.RenderTimeMs = ComputeAverage(_renderTimeSamples, _renderTimeSampleSize);
        _snapshot.PresentTimeMs = ComputeAverage(_presentTimeSamples, _presentTimeSampleSize);
        _snapshot.DiagnosticsTimeMs = ComputeAverage(_diagnosticsTimeSamples, _diagnosticsTimeSampleSize);
        _snapshot.FrameTimeMs = activeFrameTimeMs;

        const f64 now = glfwGetTime();
        if (_lastFrameTime > 0.0)
        {
            const f64 delta = now - _lastFrameTime;
            if (delta > 0.0)
            {
                const auto fps = static_cast<f32>(1.0 / delta);

                PushSample(_fpsSamples, _fpsSampleIndex, _fpsSampleSize, fps);
                PushSample(_frameTimeSamples, _frameTimeSampleIndex, _frameTimeSampleSize, activeFrameTimeMs);

                _snapshot.Fps = ComputeAverage(_fpsSamples, _fpsSampleSize);
                _snapshot.FrameTimeMs = ComputeAverage(_frameTimeSamples, _frameTimeSampleSize);
            }
        }

        _lastFrameTime = now;

        f32 workingSetMegabytes = 0.0f;
        f32 privateMegabytes = 0.0f;
        if (!TryGetProcessMemoryMegabytes(workingSetMegabytes, privateMegabytes)) return;

        _snapshot.WorkingSetMemoryMb = workingSetMegabytes;
        _snapshot.PrivateMemoryMb = privateMegabytes;
    }

    void DiagnosticsService::SetLoadedAssets(const i32 loadedAssetCount, const i64 loadedAssetsSizeBytes)
    {
        _snapshot.LoadedAssetCount = loadedAssetCount;
        _snapshot.LoadedAssetsMemoryMb = static_cast<f32>(loadedAssetsSizeBytes) * BYTES_TO_MEGABYTES;
    }

    void DiagnosticsService::SetExecutionTimes(const ExecutionTimes& executionTimes)
    {
        _rawExecutionTimes.WindowTimeMs = executionTimes.WindowTimeMs;
        _rawExecutionTimes.UpdateTimeMs = executionTimes.UpdateTimeMs;
    }

    void DiagnosticsService::SetRenderCpuTime(const f32 renderCpuTimeMs)
    {
        _rawExecutionTimes.RenderCpuTimeMs = renderCpuTimeMs;
    }

    void DiagnosticsService::SetPresentTime(const f32 presentTimeMs)
    {
        _rawExecutionTimes.PresentTimeMs = presentTimeMs;
    }

    void DiagnosticsService::SetDiagnosticsTime(const f32 diagnosticsTimeMs)
    {
        _rawExecutionTimes.DiagnosticsTimeMs = diagnosticsTimeMs;
    }

    void DiagnosticsService::ToggleDebugOverlay()
    {
        _isDebugOverlayEnabled = !_isDebugOverlayEnabled;
    }

    bool DiagnosticsService::IsDebugOverlayEnabled() const
    {
        return _isDebugOverlayEnabled;
    }

    const DiagnosticsSnapshot& DiagnosticsService::GetSnapshot() const
    {
        return _snapshot;
    }

    bool DiagnosticsService::TryGetProcessMemoryMegabytes(f32& workingSetMegabytes, f32& privateMegabytes)
    {
#ifdef _WIN32
        PROCESS_MEMORY_COUNTERS_EX counters = {};
        if (!GetProcessMemoryInfo(GetCurrentProcess(), reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&counters), sizeof(counters)))
        {
            return false;
        }

        workingSetMegabytes = static_cast<f32>(counters.WorkingSetSize) * BYTES_TO_MEGABYTES;
        privateMegabytes = static_cast<f32>(counters.PrivateUsage) * BYTES_TO_MEGABYTES;
        return true;
#else
        workingSetMegabytes = 0.0f;
        privateMegabytes = 0.0f;
        return false;
#endif
    }
}
