#include "pch.h"
#include "Stopwatch.h"

namespace rei::time
{
    void Stopwatch::Start()
    {
        _start = clock::now();
        _end = _start;
        _isRunning = true;
    }

    void Stopwatch::Restart()
    {
        Start();
    }

    void Stopwatch::Stop()
    {
        if (!_isRunning) return;

        _end = clock::now();
        _isRunning = false;
    }

    bool Stopwatch::IsRunning() const
    {
        return _isRunning;
    }

    f32 Stopwatch::ElapsedMs() const
    {
        const auto end = _isRunning ? clock::now() : _end;
        return static_cast<f32>(std::chrono::duration<f64, std::milli>(end - _start).count());
    }

    f32 Stopwatch::ElapsedSec() const
    {
        const auto end = _isRunning ? clock::now() : _end;
        return static_cast<f32>(std::chrono::duration<f64>(end - _start).count());
    }
}
