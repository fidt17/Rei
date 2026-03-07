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
        constexpr float BYTES_TO_MEGABYTES = 1.0f / (1024.0f * 1024.0f);
    }

    void DiagnosticsService::PushSample(std::array<float, SAMPLE_COUNT>& samples, i32& sampleIndex, i32& sampleSize, const float value) const
    {
        samples[sampleIndex] = value;
        sampleIndex = (sampleIndex + 1) % SAMPLE_COUNT;
        if (sampleSize < SAMPLE_COUNT)
        {
            sampleSize++;
        }
    }

    float DiagnosticsService::ComputeAverage(const std::array<float, SAMPLE_COUNT>& samples, const i32 sampleSize)
    {
        if (sampleSize <= 0) return 0.0f;

        float sum = 0.0f;
        for (i32 i = 0; i < sampleSize; i++)
        {
            sum += samples[i];
        }

        return sum / static_cast<float>(sampleSize);
    }

    void DiagnosticsService::Update()
    {
        if (_hasPendingTimingSample)
        {
            const auto renderWithoutDiagnosticsMs = _rawRenderTimeMs > _rawDiagnosticsTimeMs
                ? _rawRenderTimeMs - _rawDiagnosticsTimeMs
                : 0.0f;

            PushSample(_coreTimeSamples, _coreTimeSampleIndex, _coreTimeSampleSize, _rawCoreTimeMs);
            PushSample(_renderTimeSamples, _renderTimeSampleIndex, _renderTimeSampleSize, renderWithoutDiagnosticsMs);
            PushSample(_diagnosticsTimeSamples, _diagnosticsTimeSampleIndex, _diagnosticsTimeSampleSize, _rawDiagnosticsTimeMs);

            _snapshot.CoreTimeMs = ComputeAverage(_coreTimeSamples, _coreTimeSampleSize);
            _snapshot.RenderTimeMs = ComputeAverage(_renderTimeSamples, _renderTimeSampleSize);
            _snapshot.DiagnosticsTimeMs = ComputeAverage(_diagnosticsTimeSamples, _diagnosticsTimeSampleSize);

            _hasPendingTimingSample = false;
        }

        const double now = glfwGetTime();
        if (_lastFrameTime > 0.0)
        {
            const double delta = now - _lastFrameTime;
            if (delta > 0.0)
            {
                const auto fps = static_cast<float>(1.0 / delta);
                const auto frameTimeMs = static_cast<float>(delta * 1000.0);

                PushSample(_fpsSamples, _fpsSampleIndex, _fpsSampleSize, fps);
                PushSample(_frameTimeSamples, _frameTimeSampleIndex, _frameTimeSampleSize, frameTimeMs);

                _snapshot.Fps = ComputeAverage(_fpsSamples, _fpsSampleSize);
                _snapshot.FrameTimeMs = ComputeAverage(_frameTimeSamples, _frameTimeSampleSize);
            }
        }

        _lastFrameTime = now;

        float workingSetMegabytes = 0.0f;
        float privateMegabytes = 0.0f;
        if (!TryGetProcessMemoryMegabytes(workingSetMegabytes, privateMegabytes)) return;

        _snapshot.WorkingSetMemoryMb = workingSetMegabytes;
        _snapshot.PrivateMemoryMb = privateMegabytes;
    }

    void DiagnosticsService::SetLoadedAssets(const i32 loadedAssetCount, const i64 loadedAssetsSizeBytes)
    {
        _snapshot.LoadedAssetCount = loadedAssetCount;
        _snapshot.LoadedAssetsMemoryMb = static_cast<float>(loadedAssetsSizeBytes) * BYTES_TO_MEGABYTES;
    }

    void DiagnosticsService::SetExecutionTimes(const float coreTimeMs, const float renderTimeMs)
    {
        _rawCoreTimeMs = coreTimeMs;
        _rawRenderTimeMs = renderTimeMs;
        _hasPendingTimingSample = true;
    }

    void DiagnosticsService::SetDiagnosticsTime(const float diagnosticsTimeMs)
    {
        _rawDiagnosticsTimeMs = diagnosticsTimeMs;
        _hasPendingTimingSample = true;
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

    bool DiagnosticsService::TryGetProcessMemoryMegabytes(float& workingSetMegabytes, float& privateMegabytes)
    {
#ifdef _WIN32
        PROCESS_MEMORY_COUNTERS_EX counters = {};
        if (!GetProcessMemoryInfo(GetCurrentProcess(), reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&counters), sizeof(counters)))
        {
            return false;
        }

        workingSetMegabytes = static_cast<float>(counters.WorkingSetSize) * BYTES_TO_MEGABYTES;
        privateMegabytes = static_cast<float>(counters.PrivateUsage) * BYTES_TO_MEGABYTES;
        return true;
#else
        workingSetMegabytes = 0.0f;
        privateMegabytes = 0.0f;
        return false;
#endif
    }
}
