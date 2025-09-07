#pragma once

namespace rei::time
{
    class ScopedTimer
    {
    private:
        std::chrono::time_point<std::chrono::high_resolution_clock> _start;
        std::string _name;
        bool _stopped = false;
        bool _log = true;
 
    public:
        explicit ScopedTimer(std::string msg, bool log = true);
        
        ~ScopedTimer();

        long long Stop();
    };
}
