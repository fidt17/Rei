#pragma once

#include <array>

#include "DiagnosticsSnapshot.h"

namespace rei::common::diagnostics
{
    class DiagnosticsService
    {
    public:
        struct ExecutionTimes
        {
            f32 WindowTimeMs = 0.0f;
            f32 UpdateTimeMs = 0.0f;
            f32 RenderCpuTimeMs = 0.0f;
            f32 PresentTimeMs = 0.0f;
            f32 DiagnosticsTimeMs = 0.0f;
        };

    public:
        REI_API void Update();
        REI_API void SetLoadedAssets(i32 loadedAssetCount, i64 loadedAssetsSizeBytes);
        REI_API void SetExecutionTimes(const ExecutionTimes& executionTimes);
        REI_API void SetRenderCpuTime(f32 renderCpuTimeMs);
        REI_API void SetPresentTime(f32 presentTimeMs);
        REI_API void SetDiagnosticsTime(f32 diagnosticsTimeMs);
        REI_API void ToggleDebugOverlay();
        REI_API bool IsDebugOverlayEnabled() const;

        REI_API const DiagnosticsSnapshot& GetSnapshot() const;

    private:
        static constexpr i32 SAMPLE_COUNT = 60;

        void PushSample(std::array<f32, SAMPLE_COUNT>& samples, i32& sampleIndex, i32& sampleSize, f32 value) const;
        static f32 ComputeAverage(const std::array<f32, SAMPLE_COUNT>& samples, i32 sampleSize);

        static bool TryGetProcessMemoryMegabytes(f32& workingSetMegabytes, f32& privateMegabytes);

    private:
        DiagnosticsSnapshot _snapshot = {};
        f64 _lastFrameTime = 0.0;
        bool _isDebugOverlayEnabled = false;

        std::array<f32, SAMPLE_COUNT> _fpsSamples = {};
        std::array<f32, SAMPLE_COUNT> _frameTimeSamples = {};
        std::array<f32, SAMPLE_COUNT> _coreTimeSamples = {};
        std::array<f32, SAMPLE_COUNT> _renderTimeSamples = {};
        std::array<f32, SAMPLE_COUNT> _presentTimeSamples = {};
        std::array<f32, SAMPLE_COUNT> _diagnosticsTimeSamples = {};

        i32 _fpsSampleIndex = 0;
        i32 _frameTimeSampleIndex = 0;
        i32 _coreTimeSampleIndex = 0;
        i32 _renderTimeSampleIndex = 0;
        i32 _presentTimeSampleIndex = 0;
        i32 _diagnosticsTimeSampleIndex = 0;

        i32 _fpsSampleSize = 0;
        i32 _frameTimeSampleSize = 0;
        i32 _coreTimeSampleSize = 0;
        i32 _renderTimeSampleSize = 0;
        i32 _presentTimeSampleSize = 0;
        i32 _diagnosticsTimeSampleSize = 0;

        ExecutionTimes _rawExecutionTimes = {};
    };
}
