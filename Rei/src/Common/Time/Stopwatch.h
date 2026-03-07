#pragma once

namespace rei::time
{
    class Stopwatch
    {
    public:
        REI_API Stopwatch() = default;

        REI_API void Start();
        REI_API void Restart();
        REI_API void Stop();

        REI_API bool IsRunning() const;
        REI_API f32 ElapsedMs() const;
        REI_API f32 ElapsedSec() const;

    private:
        using clock = std::chrono::high_resolution_clock;
        using time_point = std::chrono::time_point<clock>;

    private:
        time_point _start = {};
        time_point _end = {};
        bool _isRunning = false;
    };
}
