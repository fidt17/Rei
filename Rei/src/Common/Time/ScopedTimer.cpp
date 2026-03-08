#include "pch.h"
#include "ScopedTimer.h"

rei::time::ScopedTimer::ScopedTimer(std::string msg, const bool log): _name(std::move(msg)), _log(log)
{
    _stopwatch.Start();
}

rei::time::ScopedTimer::~ScopedTimer()
{
    Stop();
}

long long rei::time::ScopedTimer::Stop()
{
    if (_stopped) return 0;
    _stopped = true;

    _stopwatch.Stop();
    const auto ms = static_cast<long long>(_stopwatch.ElapsedMs());

    if (_log)
    {
        if (ms < 1000)
        {
            LOG_DEBUG("[{}] {} ms", _name, ms)
        }
        else
        {
            const f64 seconds = ms / 1000.0;
            LOG_DEBUG("[{}] {} seconds", _name, seconds)
        }
    }

    return ms;
}
