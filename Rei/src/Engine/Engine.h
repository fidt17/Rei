#pragma once

#include <thread>

namespace rei
{
    class Engine
    {
    public:
        void Configure();
        REI_API void Start();
        REI_API void Shutdown(int exitCode);

    private:

        void OnUpdate();

        std::thread _mainThread;
        bool _mainThreadRunFlag = false;
    };
}
