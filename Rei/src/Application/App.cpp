#include "App.h"
#include <thread>
#include <chrono>

namespace rei
{
    void App::Configure()
    {
        LOG("[App] Configure")
    }

    void App::Start()
    {
        LOG("[App] Run")

        _mainThread = std::thread([&]()
        {
            _mainThreadRunFlag = true;
            while (_mainThreadRunFlag)
            {
                OnUpdate();
                std::this_thread::sleep_for(std::chrono::seconds(1));
            }
        });
    }

    void App::Shutdown(const int exitCode)
    {
        LOG("[App] Shutdown. Exit code: " + std::to_string(exitCode))

        _mainThreadRunFlag = false;
        _mainThread.join();
    }

    int Counter = 0;

    void App::OnUpdate()
    {
        LOG("[App] Counter: " + std::to_string(Counter++))
    }
}
