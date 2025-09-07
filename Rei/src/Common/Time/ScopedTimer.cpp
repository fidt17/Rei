#include "pch.h"
#include "ScopedTimer.h"

rei::time::ScopedTimer::ScopedTimer(std::string msg, const bool log): _name(std::move(msg)), _log(log)
{
    _start = std::chrono::high_resolution_clock::now();
}

rei::time::ScopedTimer::~ScopedTimer()
{
    Stop();
}

long long rei::time::ScopedTimer::Stop()
{
    if (_stopped) return 0;
    _stopped = true;

    const auto end = std::chrono::high_resolution_clock::now();
    const auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - _start);

    const long long ms = duration.count();

    if (_log)
    {
        if (ms < 1000)
        {
            LOG("[T:" + _name + "] " + STRING(ms) + " ms");
        }
        else
        {
            const double seconds = ms / 1000.0;
            LOG_WARNING("[T:" + _name + "] " + STRING(seconds) + " seconds");
        }
    }

    return ms;
}
