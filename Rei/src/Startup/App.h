#pragma once
#include <string>

#include "Common/Event.h"
#include <functional>

namespace rei
{
    class REI_API App
    {
    public:
        Event<std::function<void(const std::string&)>> MessageEvent;

        void Configure();
        void Run();
        int Shutdown(int exitCode);
        void AddLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>& logCallback);

    private:
        bool _appRunning = false;
        bool _runApp = false;
    };
}
