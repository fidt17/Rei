#pragma once

#include <thread>

namespace rei
{
    class REI_API Engine
    {
    public:
        void Configure();
        void Start();
        void Shutdown(int exitCode);

    private:

        void OnUpdate();

        std::thread _mainThread;
        bool _mainThreadRunFlag = false;
    };
}
