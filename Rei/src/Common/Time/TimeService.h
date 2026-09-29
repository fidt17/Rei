#pragma once

#include <chrono>
#include "Common/Primitives.h"

namespace rei::time
{
    // Engine-thread frame snapshot. Reads never advance the clock.
    class TimeService
    {
    public:
        REI_API TimeService();
        REI_API void Reset();
        REI_API void BeginFrame();

        REI_API f64 GetDeltaSeconds() const;
        REI_API f64 GetElapsedSeconds() const;

    private:
        using Clock = std::chrono::steady_clock;
        Clock::time_point _start;
        Clock::time_point _previousFrame;
        f64 _deltaSeconds = 0;
        f64 _elapsedSeconds = 0;
        bool _hasFrame = false;
    };
}
