#pragma once

#include "Stopwatch.h"

namespace rei::time
{
    class ScopedTimer
    {
    private:
        std::string _name;
        bool _stopped = false;
        bool _log = true;
        Stopwatch _stopwatch;
 
    public:
        REI_API explicit ScopedTimer(std::string msg, bool log = true);
        REI_API ~ScopedTimer();

        REI_API long long Stop();
    };
}
