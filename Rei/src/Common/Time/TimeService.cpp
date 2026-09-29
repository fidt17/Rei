#include "pch.h"
#include "TimeService.h"

namespace rei::time
{
    TimeService::TimeService()
    {
        Reset();
    }

    void TimeService::Reset()
    {
        _start = Clock::now();
        _previousFrame = _start;
        _deltaSeconds = 0;
        _elapsedSeconds = 0;
        _hasFrame = false;
    }

    void TimeService::BeginFrame()
    {
        const auto now = Clock::now();
        _deltaSeconds = _hasFrame ? std::chrono::duration<f64>(now - _previousFrame).count() : 0;
        _elapsedSeconds = std::chrono::duration<f64>(now - _start).count();
        _previousFrame = now;
        _hasFrame = true;
    }

    f64 TimeService::GetDeltaSeconds() const
    {
        return _deltaSeconds;
    }

    f64 TimeService::GetElapsedSeconds() const
    {
        return _elapsedSeconds;
    }
}
