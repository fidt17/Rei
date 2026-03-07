#pragma once

#include <array>

#include "DiagnosticsSnapshot.h"

namespace rei::common::diagnostics
{
    class DiagnosticsService
    {
    public:
        REI_API void Update();
        REI_API void SetLoadedAssets(i32 loadedAssetCount, i64 loadedAssetsSizeBytes);
        REI_API void SetExecutionTimes(float coreTimeMs, float renderTimeMs);
        REI_API void SetDiagnosticsTime(float diagnosticsTimeMs);
        REI_API void ToggleDebugOverlay();
        REI_API bool IsDebugOverlayEnabled() const;

        REI_API const DiagnosticsSnapshot& GetSnapshot() const;

    private:
        static constexpr i32 SAMPLE_COUNT = 60;

        void PushSample(std::array<float, SAMPLE_COUNT>& samples, i32& sampleIndex, i32& sampleSize, float value) const;
        static float ComputeAverage(const std::array<float, SAMPLE_COUNT>& samples, i32 sampleSize);

        static bool TryGetProcessMemoryMegabytes(float& workingSetMegabytes, float& privateMegabytes);

    private:
        DiagnosticsSnapshot _snapshot = {};
        double _lastFrameTime = 0.0;
        bool _isDebugOverlayEnabled = false;

        std::array<float, SAMPLE_COUNT> _fpsSamples = {};
        std::array<float, SAMPLE_COUNT> _frameTimeSamples = {};
        std::array<float, SAMPLE_COUNT> _coreTimeSamples = {};
        std::array<float, SAMPLE_COUNT> _renderTimeSamples = {};
        std::array<float, SAMPLE_COUNT> _diagnosticsTimeSamples = {};

        i32 _fpsSampleIndex = 0;
        i32 _frameTimeSampleIndex = 0;
        i32 _coreTimeSampleIndex = 0;
        i32 _renderTimeSampleIndex = 0;
        i32 _diagnosticsTimeSampleIndex = 0;

        i32 _fpsSampleSize = 0;
        i32 _frameTimeSampleSize = 0;
        i32 _coreTimeSampleSize = 0;
        i32 _renderTimeSampleSize = 0;
        i32 _diagnosticsTimeSampleSize = 0;

        float _rawRenderTimeMs = 0.0f;
        float _rawCoreTimeMs = 0.0f;
        float _rawDiagnosticsTimeMs = 0.0f;
        bool _hasPendingTimingSample = false;
    };
}
